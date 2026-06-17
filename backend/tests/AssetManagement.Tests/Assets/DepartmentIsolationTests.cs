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

        var dept1 = await Post<ApiResult<DepartmentDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "研发部",
            Code = $"RD_{Guid.NewGuid():N}"[..10]
        });
        var dept2 = await Post<ApiResult<DepartmentDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "市场部",
            Code = $"MK_{Guid.NewGuid():N}"[..10]
        });

        // 创建两个部门管理员用户
        var deptAdmin1 = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = $"DA{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}",
            Name = "研发部经理",
            Password = "123456",
            DepartmentId = dept1.Data!.Id
        });
        var deptAdmin2 = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = $"DA{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000 + 1}",
            Name = "市场部经理",
            Password = "123456",
            DepartmentId = dept2.Data!.Id
        });

        // 给用户分配dept_admin角色
        var roles = await _client.GetFromJsonAsync<ApiResult<List<RoleDto>>>("/api/roles");
        var deptAdminRole = roles!.Data!.Single(r => r.Code == "dept_admin");
        await Post<ApiResult<UserDto>>($"/api/users/{deptAdmin1.Data!.Id}/roles", new AssignRoleRequest
        {
            RoleIds = new[] { deptAdminRole.Id }
        });
        await Post<ApiResult<UserDto>>($"/api/users/{deptAdmin2.Data!.Id}/roles", new AssignRoleRequest
        {
            RoleIds = new[] { deptAdminRole.Id }
        });

        // 创建资产分类
        var category = await CreateCategory();

        // 在两个部门各创建一个资产
        var asset1 = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "研发部服务器",
            CategoryId = category.Id,
            Price = 10000,
            DepartmentId = dept1.Data.Id
        });
        var asset2 = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "市场部投影仪",
            CategoryId = category.Id,
            Price = 5000,
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

    private async Task<CategoryNodeDto> CreateCategory()
    {
        var rootSeg = Unique("CAT");
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            Name = "测试分类",
            CodeSeg = rootSeg
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            Name = "末级分类",
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
