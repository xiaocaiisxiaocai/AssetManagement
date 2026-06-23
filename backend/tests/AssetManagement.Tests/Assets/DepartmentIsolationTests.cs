using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Assets;

public class DepartmentIsolationTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public DepartmentIsolationTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeptAdmin_only_sees_own_department_assets()
    {
        // 以管理员身份登录,创建两个部门
        var adminToken = await LoginAsAdmin();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 获取所有角色，并找到部门管理员角色
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var deptAdminRole = roles!.Data!.Items.Single(r => r.Code == "dept_admin");

        var dept1 = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "研发部"
        });
        var dept2 = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "市场部"
        });

        // 创建两个部门管理员用户，直接传入 RoleIds
        var deptAdmin1 = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = $"DA{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}",
            Name = "研发部经理",
            Password = "123456",
            DepartmentId = dept1.Data!.Id,
            RoleIds = new[] { deptAdminRole.Id }
        });
        var deptAdmin2 = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = $"DA{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000 + 1}",
            Name = "市场部经理",
            Password = "123456",
            DepartmentId = dept2.Data!.Id,
            RoleIds = new[] { deptAdminRole.Id }
        });

        // 创建资产分类
        var category = await CreateCategory();

        // 在两个部门各创建一个资产
        var asset1 = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "研发部服务器",
            CategoryId = category.Id,
            DepartmentId = dept1.Data.Id
        });
        var asset2 = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "市场部投影仪",
            CategoryId = category.Id,
            DepartmentId = dept2.Data.Id
        });

        // 作为研发部部门管理员登录
        var token1 = await Login(deptAdmin1.Data.EmployeeNo, "123456");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);

        // 查询资产列表,应该只能看到研发部的资产
        var list1 = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>("/api/assets");
        list1!.Data!.Items.Should().Contain(a => a.Id == asset1.Data!.Id);
        list1.Data.Items.Should().NotContain(a => a.Id == asset2.Data!.Id);

        // 作为市场部部门管理员登录
        var token2 = await Login(deptAdmin2.Data.EmployeeNo, "123456");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        // 查询资产列表,应该只能看到市场部的资产
        var list2 = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>("/api/assets");
        list2!.Data!.Items.Should().Contain(a => a.Id == asset2.Data!.Id);
        list2.Data.Items.Should().NotContain(a => a.Id == asset1.Data!.Id);

        // 作为超级管理员登录
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 超级管理员应该能看到所有资产
        var listAdmin = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>("/api/assets");
        listAdmin!.Data!.Items.Should().Contain(a => a.Id == asset1.Data!.Id);
        listAdmin.Data.Items.Should().Contain(a => a.Id == asset2.Data!.Id);
    }

    // P0-1:部门管理员不得通过详情/编辑/删除越权访问其他部门资产(IDOR)
    [Fact]
    public async Task DeptAdmin_cannot_access_other_department_asset()
    {
        var adminToken = await LoginAsAdmin();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var deptAdminRole = roles!.Data!.Items.Single(r => r.Code == "dept_admin");

        var dept1 = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "研发部"
        });
        var dept2 = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "市场部"
        });

        var deptAdmin1 = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = $"DX{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}",
            Name = "研发部经理X",
            Password = "123456",
            DepartmentId = dept1.Data!.Id,
            RoleIds = new[] { deptAdminRole.Id }
        });

        var category = await CreateCategory();
        var asset2 = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "市场部资产X",
            CategoryId = category.Id,
            DepartmentId = dept2.Data!.Id
        });

        // 以研发部管理员登录,尝试越权访问市场部资产
        var token1 = await Login(deptAdmin1.Data!.EmployeeNo, "123456");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);

        var detail = await _client.GetFromJsonAsync<ApiResult<AssetDetailDto>>($"/api/assets/{asset2.Data!.Id}/detail");
        detail!.Code.Should().NotBe(0, "部门管理员不应能查看其他部门资产详情");

        var updateRes = await _client.PutAsJsonAsync($"/api/assets/{asset2.Data.Id}", new UpdateAssetRequest
        {
            Name = "被越权修改",
            CategoryId = category.Id,
            DepartmentId = dept2.Data.Id,
            Quantity = 1,
            Status = AssetStatus.Available
        });
        var updateBody = await updateRes.Content.ReadFromJsonAsync<ApiResult<AssetDto>>();
        updateBody!.Code.Should().NotBe(0, "部门管理员不应能修改其他部门资产");

        // 删除:dept_admin 角色无 asset:delete 权限,应在权限层即被拦截(403)
        var deleteRes = await _client.DeleteAsync($"/api/assets/{asset2.Data.Id}");
        deleteRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden, "部门管理员无删除权限");
    }

    private async Task<CategoryNodeDto> CreateCategory()
    {
        var rootSeg = Unique("CAT");
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = rootSeg
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = Unique("LEAF")
        });
        return child.Data!;
    }

    private async Task<string> LoginAsAdmin()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        return body.Data!.Token;
    }

    private async Task<string> Login(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password
        });
        return body.Data!.Token;
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];
}
