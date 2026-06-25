using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Rbac;

public class RbacManagementApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public RbacManagementApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_user_then_list_can_find_it()
    {
        await Login();
        var employeeNo = Unique("u");

        var created = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "测试用户",
            RoleIds = Array.Empty<int>()
        });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        created.Code.Should().Be(0);
        list!.Data!.Items.Should().Contain(x => x.EmployeeNo == employeeNo && x.Name == "测试用户");
    }

    [Fact]
    public async Task Create_permission_then_list_can_find_it()
    {
        await Login();
        var permissionCode = Unique("asset:archive");

        await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = permissionCode,
            Name = "资产归档",
            Module = "asset"
        });
        var list = await _client.GetFromJsonAsync<ApiResult<List<PermissionDto>>>("/api/permissions");

        list!.Data!.Should().Contain(x => x.Code == permissionCode);
    }

    [Fact]
    public async Task Create_menu_then_tree_can_find_it()
    {
        await Login();
        var menuName = Unique("DemoRoot");

        await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            ParentId = null,
            Name = menuName,
            Title = "演示菜单",
            Path = $"/demo/{menuName}",
            Component = "BasicLayout",
            Sort = 99,
            Type = "menu"
        });
        var tree = await _client.GetFromJsonAsync<ApiResult<List<MenuDto>>>("/api/menus");

        tree!.Data!.Should().Contain(x => x.Name == menuName);
    }

    [Fact]
    public async Task Set_role_permission_then_user_info_contains_permission_code()
    {
        await Login();
        var permissionCode = Unique("demo:run");
        var roleCode = Unique("demo_role");
        var employeeNo = Unique("u");
        var password = employeeNo[^6..];

        var permission = await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = permissionCode,
            Name = "演示执行",
            Module = "demo"
        });
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = roleCode,
            Name = "演示角色",
            IsActive = true
        });
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "演示员工",
            RoleIds = new[] { role.Data!.Id }
        });

        await Put<ApiResult<RoleDto>>($"/api/roles/{role.Data.Id}/permissions", new
        {
            permissionIds = new[] { permission.Data!.Id }
        });
        var tokenBody = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = user.Data!.EmployeeNo,
            password
        });
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenBody.Data!.Token);

        var info = await _client.GetFromJsonAsync<ApiResult<UserInfoDto>>("/api/auth/user-info");

        info!.Data!.Permissions.Should().Contain(permissionCode);
    }

    [Fact]
    public async Task Set_role_menu_accepts_frontend_request_body()
    {
        await Login();
        var roleCode = Unique("menu_role");
        var menuName = Unique("MenuDemo");

        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = roleCode,
            Name = "菜单授权角色",
            IsActive = true
        });
        var menu = await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            Name = menuName,
            Title = "菜单授权演示",
            Path = $"/demo/{menuName}",
            Component = "/demo/index",
            Sort = 100,
            Type = "menu"
        });

        var updated = await Put<ApiResult<RoleDto>>($"/api/roles/{role.Data!.Id}/menus", new
        {
            menuIds = new[] { menu.Data!.Id }
        });

        updated.Data!.MenuIds.Should().Contain(menu.Data.Id);
    }

    [Fact]
    public async Task Set_user_status_is_idempotent()
    {
        await Login();
        var employeeNo = Unique("u");
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "状态测试用户",
            RoleIds = Array.Empty<int>()
        });

        await Post<ApiResult<object?>>($"/api/users/{user.Data!.Id}/toggle-status", new { isActive = false });
        await Post<ApiResult<object?>>($"/api/users/{user.Data.Id}/toggle-status", new { isActive = false });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        list!.Data!.Items.Single().IsActive.Should().BeFalse();
    }

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}";
}
