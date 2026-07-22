using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using FluentAssertions;

namespace AssetManagement.Tests.Rbac;

public class SupervisorMaintenanceApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public SupervisorMaintenanceApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Supervisor_can_maintain_categories_but_cannot_purge_them()
    {
        await Login("TEST-SUPERVISOR");
        var created = await PostSuccess<CategoryNodeDto>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueSegment(),
            Remark = "主管新增"
        });

        var updatedSegment = UniqueSegment();
        var updated = await PutSuccess<CategoryNodeDto>($"/api/categories/{created.Id}", new UpdateCategoryRequest
        {
            CodeSeg = updatedSegment
        });
        updated.CodeSeg.Should().Be(updatedSegment);
        updated.Code.Should().Be(updatedSegment);

        await DeleteSuccess($"/api/categories/{created.Id}");
        await PostSuccess<object?>($"/api/categories/{created.Id}/restore", new { });

        await DeleteSuccess($"/api/categories/{created.Id}");
        var purgeResponse = await _client.DeleteAsync($"/api/categories/{created.Id}/purge");
        purgeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Supervisor_can_crud_departments_across_the_organization()
    {
        await Login("TEST-SUPERVISOR");
        var parent = await PostSuccess<DepartmentNodeDto>("/api/departments", new CreateDepartmentRequest
        {
            Name = $"主管维护部门-{Guid.NewGuid():N}"
        });
        var child = await PostSuccess<DepartmentNodeDto>("/api/departments", new CreateDepartmentRequest
        {
            ParentId = parent.Id,
            Name = $"主管维护下级-{Guid.NewGuid():N}"
        });

        var updatedName = $"主管已编辑部门-{Guid.NewGuid():N}";
        var updated = await PutSuccess<DepartmentNodeDto>($"/api/departments/{parent.Id}", new UpdateDepartmentRequest
        {
            Name = updatedName,
            IsActive = true
        });
        updated.Name.Should().Be(updatedName);

        var tree = await _client.GetFromJsonAsync<ApiResult<List<DepartmentNodeDto>>>("/api/departments/tree");
        Flatten(tree!.Data!).Select(x => x.Id).Should().Contain(new[] { parent.Id, child.Id });

        await DeleteSuccess($"/api/departments/{child.Id}");
        await DeleteSuccess($"/api/departments/{parent.Id}");
    }

    [Fact]
    public async Task Supervisor_can_crud_employees_across_departments_but_cannot_manage_privileged_users()
    {
        await Login();
        var roles = (await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?pageSize=100"))!
            .Data!.Items;
        var employeeRole = roles.Single(x => x.Code == "employee");
        var administrator = (await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            "/api/users?keyword=1001"))!.Data!.Items.Single();
        var supervisor = (await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            "/api/users?keyword=TEST-SUPERVISOR"))!.Data!.Items.Single();
        var outsideDepartment = await PostSuccess<DepartmentNodeDto>("/api/departments", new CreateDepartmentRequest
        {
            Name = $"范围外部门-{Guid.NewGuid():N}"
        });
        var outsideEmployee = await PostSuccess<UserDto>("/api/users", new CreateUserRequest
        {
            EmployeeNo = UniqueEmployeeNo("outside"),
            Name = "范围外员工",
            DepartmentId = outsideDepartment.Id,
            RoleIds = new[] { employeeRole.Id }
        });

        await Login("TEST-SUPERVISOR");
        var visibleOutsideUser = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword={outsideEmployee.EmployeeNo}");
        visibleOutsideUser!.Data!.Items.Should().ContainSingle(x =>
            x.Id == outsideEmployee.Id && x.CanManage);

        var created = await PostSuccess<UserDto>("/api/users", new CreateUserRequest
        {
            EmployeeNo = UniqueEmployeeNo("cross-create"),
            Name = "跨部门新增员工",
            DepartmentId = outsideDepartment.Id,
            RoleIds = Array.Empty<int>()
        });
        created.RoleIds.Should().Equal(employeeRole.Id);
        created.CanManage.Should().BeTrue();

        var updated = await PutSuccess<UserDto>($"/api/users/{created.Id}", new UpdateUserRequest
        {
            Name = "跨部门新增员工已编辑",
            DepartmentId = outsideDepartment.Id,
            RoleIds = new[] { employeeRole.Id }
        });
        updated.Name.Should().Be("跨部门新增员工已编辑");
        await DeleteSuccess($"/api/users/{created.Id}");

        var updatedOutsideEmployee = await PutSuccess<UserDto>($"/api/users/{outsideEmployee.Id}", new UpdateUserRequest
        {
            Name = "跨部门员工已编辑",
            DepartmentId = outsideDepartment.Id,
            RoleIds = new[] { employeeRole.Id }
        });
        updatedOutsideEmployee.Name.Should().Be("跨部门员工已编辑");
        await DeleteSuccess($"/api/users/{outsideEmployee.Id}");
        await AssertForbidden(await _client.DeleteAsync($"/api/users/{administrator.Id}"), "无权管理该用户");
        await AssertForbidden(await _client.DeleteAsync($"/api/users/{supervisor.Id}"), "无权管理该用户");
    }

    private async Task Login(string employeeNo = "1001")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!;
        result.Code.Should().Be(0);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Data!.Token);
    }

    private async Task<T> PostSuccess<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!;
        result.Code.Should().Be(0);
        return result.Data!;
    }

    private async Task<T> PutSuccess<T>(string url, object body)
    {
        var response = await _client.PutAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!;
        result.Code.Should().Be(0);
        return result.Data!;
    }

    private async Task DeleteSuccess(string url)
    {
        var response = await _client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<object?>>())!;
        result.Code.Should().Be(0);
    }

    private static async Task AssertForbidden(HttpResponseMessage response, string expectedMessage)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<object?>>())!;
        result.Code.Should().Be(4032);
        result.Message.Should().Be(expectedMessage);
    }

    private static string UniqueEmployeeNo(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string UniqueSegment() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static IEnumerable<DepartmentNodeDto> Flatten(IEnumerable<DepartmentNodeDto> nodes)
        => nodes.SelectMany(x => new[] { x }.Concat(Flatten(x.Children)));
}
