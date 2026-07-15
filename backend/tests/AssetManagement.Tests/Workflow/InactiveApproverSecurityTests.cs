using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Workflow;

/// <summary>
/// 审批人账号或角色在 JWT 签发后被停用时，旧令牌不得继续处理审批。
/// </summary>
public class InactiveApproverSecurityTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public InactiveApproverSecurityTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Approver_cannot_approve_with_token_issued_before_account_was_disabled()
    {
        var scenario = await CreatePendingSupervisorFlow();
        var approverClient = _factory.CreateClient();
        await Authenticate(approverClient, scenario.SupervisorEmployeeNo);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users
                .Where(x => x.Id == scenario.SupervisorId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false));
        }

        var response = await approverClient.PostAsJsonAsync(
            $"/api/approvals/{scenario.FlowId}/approve",
            new ApprovalActionRequest { Opinion = "账号停用后不应通过" });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body!.Code.Should().Be(4011);
        await AssertFlowRemainsPending(scenario.FlowId);
    }

    [Fact]
    public async Task Approver_cannot_approve_with_token_issued_before_role_was_disabled()
    {
        var scenario = await CreatePendingSupervisorFlow();
        var approverClient = _factory.CreateClient();
        await Authenticate(approverClient, scenario.SupervisorEmployeeNo);

        try
        {
            await SetSupervisorRoleActive(false);

            var response = await approverClient.PostAsJsonAsync(
                $"/api/approvals/{scenario.FlowId}/approve",
                new ApprovalActionRequest { Opinion = "角色停用后不应通过" });
            var body = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            body!.Code.Should().Be(4012);
            await AssertFlowRemainsPending(scenario.FlowId);
        }
        finally
        {
            await SetSupervisorRoleActive(true);
        }
    }

    private async Task<ApprovalScenario> CreatePendingSupervisorFlow()
    {
        var adminClient = _factory.CreateClient();
        await Authenticate(adminClient, "1001");

        var roles = await Get<ApiResult<PagedResult<RoleDto>>>(adminClient, "/api/roles");
        var supervisorRole = roles.Data!.Items.Single(x => x.Code == "supervisor");
        var employeeRole = roles.Data.Items.Single(x => x.Code == "employee");

        var department = await Post<ApiResult<DepartmentNodeDto>>(adminClient, "/api/departments",
            new CreateDepartmentRequest { Name = Unique("停用审批部门") });
        var supervisor = await Post<ApiResult<UserDto>>(adminClient, "/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("SUP"),
            Name = Unique("停用审批主管"),
            Password = "123456",
            DepartmentId = department.Data!.Id,
            RoleIds = new[] { supervisorRole.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>(adminClient, $"/api/departments/{department.Data.Id}",
            new UpdateDepartmentRequest
            {
                Name = department.Data.Name,
                ManagerId = supervisor.Data!.Id,
                IsActive = true
            });

        var applicant = await Post<ApiResult<UserDto>>(adminClient, "/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("APP"),
            Name = Unique("停用审批申请人"),
            Password = "123456",
            DepartmentId = department.Data.Id,
            SupervisorId = supervisor.Data.Id,
            RoleIds = new[] { employeeRole.Id }
        });
        var asset = await CreateAsset(adminClient, department.Data.Id);

        var applicantClient = _factory.CreateClient();
        await Authenticate(applicantClient, applicant.Data!.EmployeeNo);
        var flow = await Post<ApiResult<ApprovalFlowDto>>(applicantClient, "/api/approvals",
            new StartApprovalRequest
            {
                BizType = "borrow",
                AssetId = asset.Id,
                Reason = "验证停用身份不能审批",
                ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
            });

        flow.Data!.Status.Should().Be("pending");
        return new ApprovalScenario(flow.Data.Id, supervisor.Data.Id, supervisor.Data.EmployeeNo);
    }

    private async Task<AssetDto> CreateAsset(HttpClient client, int departmentId)
    {
        var root = await Post<ApiResult<CategoryNodeDto>>(client, "/api/categories",
            new CreateCategoryRequest { CodeSeg = UniqueCodeSeg() });
        var child = await Post<ApiResult<CategoryNodeDto>>(client, "/api/categories",
            new CreateCategoryRequest { ParentId = root.Data!.Id, CodeSeg = UniqueCodeSeg() });
        var asset = await Post<ApiResult<AssetDto>>(client, "/api/assets", new CreateAssetRequest
        {
            Name = Unique("停用审批测试资产"),
            CategoryId = child.Data!.Id,
            DepartmentId = departmentId
        });
        return asset.Data!;
    }

    private async Task AssertFlowRemainsPending(int flowId)
    {
        var adminClient = _factory.CreateClient();
        await Authenticate(adminClient, "1001");
        var flow = await Get<ApiResult<ApprovalFlowDto>>(adminClient, $"/api/approvals/{flowId}");

        flow.Code.Should().Be(0);
        flow.Data!.Status.Should().Be("pending");
    }

    private async Task SetSupervisorRoleActive(bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Roles
            .Where(x => x.Code == "supervisor")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, isActive));
    }

    private static async Task Authenticate(HttpClient client, string employeeNo)
    {
        var login = await Post<ApiResult<LoginResponse>>(client, "/api/auth/login",
            new { employeeNo, password = "123456" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Data!.Token);
    }

    private static async Task<T> Get<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> Put<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private sealed record ApprovalScenario(int FlowId, int SupervisorId, string SupervisorEmployeeNo);
}
