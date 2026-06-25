using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.TestMaterials;
using FluentAssertions;
using Xunit;

namespace AssetManagement.Tests.TestMaterials;

public class MaterialFlowApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    public MaterialFlowApiTests(TestWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Transfer_with_switch_off_changes_custodian_directly()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("直接转移项目");
        var transferee = await CreateUser("0902", "受让人乙");
        var material = await CreateMaterial(project.Id, "直转样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "测试直接转移"
        });
        flow.Data!.DirectTransfer.Should().BeTrue();
        flow.Data.Status.Should().Be("approved");

        var got = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        got!.Data!.CustodianId.Should().Be(transferee.Id);
    }

    [Fact]
    public async Task Transfer_with_switch_on_creates_pending_flow_then_approval_changes_custodian()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("审批转移项目");
        var transferee = await CreateUser("0903", "受让人丙");
        var material = await CreateMaterial(project.Id, "审批样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "测试审批转移"
        });
        flow.Data!.Status.Should().Be("pending");
        flow.Data.DirectTransfer.Should().BeFalse();

        // 发起后保管人尚未变更
        var before = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        before!.Data!.CustodianId.Should().NotBe(transferee.Id);
        before.Data.HasPendingFlow.Should().BeTrue();

        // admin 审批通过(admin 绕过审批人校验)
        var approved = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/approve", new MaterialApprovalRequest { Opinion = "同意" });
        approved.Data!.Status.Should().Be("approved");

        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(transferee.Id);
    }

    [Fact]
    public async Task Transfer_with_switch_on_reject_keeps_custodian()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("驳回项目");
        var transferee = await CreateUser("0904", "受让人丁");
        var material = await CreateMaterial(project.Id, "驳回样品");
        var originalCustodian = material.CustodianId;

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id, TransfereeId = transferee.Id, Reason = "将被驳回"
        });

        var rejected = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data!.Id}/reject", new MaterialRejectRequest { Reason = "不同意" });
        rejected.Data!.Status.Should().Be("rejected");

        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(originalCustodian);
    }

    // ===== 辅助方法 =====
    private async Task SetApprovalSwitch(bool enabled)
    {
        var res = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "material.transfer.approval.enabled",
                Value = enabled ? "true" : "false",
                Description = "是否启用测试料件转移审批(false=直接转移)"
            }
        });
        res.EnsureSuccessStatusCode();
    }

    private async Task<TestProjectDto> CreateProject(string name)
        => (await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest { Name = name })).Data!;

    private async Task<TestMaterialDto> CreateMaterial(int projectId, string name)
        => (await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = name, ProjectId = projectId
        })).Data!;

    private async Task<UserDto> CreateUser(string employeeNo, string name)
        => (await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo, Name = name, RoleIds = Array.Empty<int>()
        })).Data!;

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task<T> Post<T>(string url, object payload)
    {
        var res = await _client.PostAsJsonAsync(url, payload);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
