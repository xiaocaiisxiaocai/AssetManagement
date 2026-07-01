using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
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
        var roleId = await CreateRoleId();

        var created = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "测试用户",
            RoleIds = new[] { roleId }
        });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        created.Code.Should().Be(0);
        list!.Data!.Items.Should().Contain(x => x.EmployeeNo == employeeNo && x.Name == "测试用户");
    }

    [Fact]
    public async Task User_list_returns_role_names()
    {
        await Login();
        var employeeNo = Unique("u");
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = "列表展示角色",
            IsActive = true
        });

        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "角色展示用户",
            RoleIds = new[] { role.Data!.Id }
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        list!.Data!.Items.Single().RoleNames.Should().Equal("列表展示角色");
    }

    [Fact]
    public async Task Create_user_without_password_uses_default_123456()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();

        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "默认密码用户",
            RoleIds = new[] { roleId }
        });

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });

        login.Code.Should().Be(0);
        login.Data!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_user_requires_role()
    {
        await Login();

        var created = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("u"),
            Name = "无角色用户",
            RoleIds = Array.Empty<int>()
        });

        created.Code.Should().Be(4001);
        created.Message.Should().Be("请选择角色");
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
        var password = "123456";

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
    public async Task Update_role_status_keeps_existing_permissions_and_menus()
    {
        await Login();
        var roleCode = Unique("role_status");
        var permissionCode = Unique("role:status");
        var menuName = Unique("RoleStatusMenu");

        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = roleCode,
            Name = "角色状态测试",
            IsActive = true
        });
        var permission = await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = permissionCode,
            Name = "角色状态权限",
            Module = "role"
        });
        var menu = await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            Name = menuName,
            Title = "角色状态菜单",
            Path = $"/demo/{menuName}",
            Component = "/demo/index",
            Sort = 101,
            Type = "menu"
        });

        await Put<ApiResult<RoleDto>>($"/api/roles/{role.Data!.Id}/permissions", new
        {
            permissionIds = new[] { permission.Data!.Id }
        });
        await Put<ApiResult<RoleDto>>($"/api/roles/{role.Data.Id}/menus", new
        {
            menuIds = new[] { menu.Data!.Id }
        });

        var updated = await Put<ApiResult<RoleDto>>($"/api/roles/{role.Data.Id}", new
        {
            name = "角色状态测试-禁用",
            isActive = false
        });

        updated.Data!.IsActive.Should().BeFalse();
        updated.Data.PermissionIds.Should().Contain(permission.Data.Id);
        updated.Data.MenuIds.Should().Contain(menu.Data.Id);
    }

    [Fact]
    public async Task Set_user_status_is_idempotent()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "状态测试用户",
            RoleIds = new[] { roleId }
        });

        await Post<ApiResult<object?>>($"/api/users/{user.Data!.Id}/toggle-status", new { isActive = false });
        await Post<ApiResult<object?>>($"/api/users/{user.Data.Id}/toggle-status", new { isActive = false });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        list!.Data!.Items.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Disabled_user_cannot_login()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "禁用登录用户",
            RoleIds = new[] { roleId }
        });
        await Post<ApiResult<object?>>($"/api/users/{user.Data!.Id}/toggle-status", new { isActive = false });

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });

        login.Code.Should().Be(4011);
        login.Message.Should().Be("账号已禁用，请联系系统管理员");
        login.Data.Should().BeNull();
    }

    [Fact]
    public async Task User_with_only_disabled_role_cannot_login()
    {
        await Login();
        var employeeNo = Unique("u");
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("disabled_role"),
            Name = "禁用角色",
            IsActive = false
        });
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "禁用角色用户",
            RoleIds = new[] { role.Data!.Id }
        });

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });

        login.Code.Should().Be(4012);
        login.Message.Should().Be("账号角色已禁用，请联系系统管理员");
        login.Data.Should().BeNull();
    }

    [Fact]
    public async Task Department_admin_in_inactive_department_cannot_login()
    {
        await Login();
        var employeeNo = Unique("u");
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?pageSize=100");
        var role = roles!.Data!.Items.Single(x => x.Code == "dept_admin");
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = 1,
            Name = "已停用登录部门"
        });
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "停用部门管理员",
            DepartmentId = department.Data!.Id,
            RoleIds = new[] { role.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{department.Data.Id}", new UpdateDepartmentRequest
        {
            ManagerId = user.Data!.Id,
            Name = department.Data.Name,
            IsActive = false
        });

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });

        login.Code.Should().Be(4013);
        login.Message.Should().Be("所属部门已停用，请联系系统管理员");
        login.Data.Should().BeNull();
    }

    [Fact]
    public async Task Reset_password_uses_default_123456()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "重置密码用户",
            Password = "old-password",
            RoleIds = new[] { roleId }
        });

        await Post<ApiResult<object?>>($"/api/users/{user.Data!.Id}/reset-password", new { });
        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });

        login.Code.Should().Be(0);
        login.Data!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Delete_user_removes_user_from_list()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "待删除用户",
            RoleIds = new[] { roleId }
        });

        var deleted = await Delete<ApiResult<object?>>($"/api/users/{user.Data!.Id}");
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        deleted.Code.Should().Be(0);
        list!.Data!.Items.Should().NotContain(x => x.Id == user.Data.Id);
    }

    [Fact]
    public async Task Delete_last_admin_user_is_blocked()
    {
        await Login();
        var admins = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>("/api/users?keyword=1001");
        var admin = admins!.Data!.Items.Single(x => x.EmployeeNo == "1001");

        var deleted = await Delete<ApiResult<object?>>($"/api/users/{admin.Id}");

        deleted.Code.Should().Be(4094);
        deleted.Message.Should().Be("至少保留一个系统管理员");
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
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Delete<T>(string url)
    {
        var res = await _client.DeleteAsync(url);
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}";

    private async Task<int> CreateRoleId()
    {
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = "测试角色",
            IsActive = true
        });
        return role.Data!.Id;
    }
}
