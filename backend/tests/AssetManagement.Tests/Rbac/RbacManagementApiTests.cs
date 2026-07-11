using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using AssetManagement.Application.Assets;
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
    public async Task User_options_returns_only_minimal_fields_for_active_users()
    {
        await Login();
        var employeeNo = Unique("option");
        var roleId = await CreateRoleId();
        var created = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "选项用户",
            Email = "private@example.local",
            Phone = "13800000000",
            RoleIds = new[] { roleId }
        });

        var options = await _client.GetFromJsonAsync<ApiResult<List<UserOptionDto>>>($"/api/users/options?keyword={employeeNo}");

        options!.Data.Should().ContainSingle(x => x.Id == created.Data!.Id && x.EmployeeNo == employeeNo && x.Name == "选项用户");
        typeof(UserOptionDto).GetProperties().Select(x => x.Name)
            .Should().BeEquivalentTo("Id", "EmployeeNo", "Name", "DepartmentName");

        await Post<ApiResult<object?>>($"/api/users/{created.Data!.Id}/toggle-status", new SetUserStatusRequest { IsActive = false });
        var afterDisable = await _client.GetFromJsonAsync<ApiResult<List<UserOptionDto>>>($"/api/users/options?keyword={employeeNo}");
        afterDisable!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_user_with_invalid_role_rolls_back_user_row()
    {
        await Login();
        var employeeNo = Unique("rollback");

        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "事务回滚用户",
            RoleIds = new[] { int.MaxValue }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiResult<object?>>();
        error!.Code.Should().Be(4042);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");
        list!.Data!.Items.Should().BeEmpty("角色关联失败时用户主记录也必须回滚");
    }

    [Fact]
    public async Task Create_role_with_invalid_permission_rolls_back_role_row()
    {
        await Login();
        var code = Unique("rollback-role");

        var response = await _client.PostAsJsonAsync("/api/roles", new RoleDto
        {
            Code = code,
            Name = Unique("事务回滚角色"),
            PermissionIds = new[] { int.MaxValue }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiResult<object?>>();
        error!.Code.Should().Be(4043);
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?page=1&pageSize=200");
        roles!.Data!.Items.Should().NotContain(x => x.Code == code, "权限关联失败时角色主记录也必须回滚");
    }

    [Fact]
    public async Task Role_list_filters_by_code_or_name_keyword()
    {
        await Login();
        var marker = Guid.NewGuid().ToString("N");
        var matching = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = $"role-{marker}",
            Name = $"关键字角色-{marker}",
            IsActive = true
        });
        await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("unmatched-role"),
            Name = Unique("其他角色"),
            IsActive = true
        });

        var byCode = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>($"/api/roles?keyword={marker}&pageSize=20");
        var byName = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>($"/api/roles?keyword={Uri.EscapeDataString($"关键字角色-{marker}")}&pageSize=20");

        byCode!.Data!.Items.Should().ContainSingle(x => x.Id == matching.Data!.Id);
        byName!.Data!.Items.Should().ContainSingle(x => x.Id == matching.Data!.Id);
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
    public async Task User_list_returns_department_name()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        var manager = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("mgr"),
            Name = "用户部门负责人",
            RoleIds = new[] { roleId }
        });
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Data!.Id,
            Name = Unique("用户部门")
        });

        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "部门展示用户",
            DepartmentId = department.Data!.Id,
            RoleIds = new[] { roleId }
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");

        list!.Data!.Items.Single().DepartmentName.Should().Be(department.Data.Name);
    }

    [Fact]
    public async Task User_list_can_filter_by_department()
    {
        await Login();
        var roleId = await CreateRoleId();
        var targetDepartment = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("筛选部门A")
        });
        var otherDepartment = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("筛选部门B")
        });
        var targetEmployeeNo = Unique("u");
        var otherEmployeeNo = Unique("u");
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            DepartmentId = targetDepartment.Data!.Id,
            EmployeeNo = targetEmployeeNo,
            Name = "部门筛选用户",
            RoleIds = new[] { roleId }
        });
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            DepartmentId = otherDepartment.Data!.Id,
            EmployeeNo = otherEmployeeNo,
            Name = "部门筛选用户",
            RoleIds = new[] { roleId }
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword=部门筛选用户&departmentId={targetDepartment.Data.Id}&pageSize=20");

        list!.Data!.Items.Select(x => x.EmployeeNo).Should().Equal(targetEmployeeNo);
    }

    [Fact]
    public async Task User_list_can_filter_by_role()
    {
        await Login();
        var targetRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("筛选角色A"),
            IsActive = true
        });
        var otherRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("筛选角色B"),
            IsActive = true
        });
        var targetEmployeeNo = Unique("u");
        var otherEmployeeNo = Unique("u");
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = targetEmployeeNo,
            Name = "角色筛选用户",
            RoleIds = new[] { targetRole.Data!.Id }
        });
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = otherEmployeeNo,
            Name = "角色筛选用户",
            RoleIds = new[] { otherRole.Data!.Id }
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword=角色筛选用户&roleId={targetRole.Data.Id}&pageSize=20");

        list!.Data!.Items.Select(x => x.EmployeeNo).Should().Equal(targetEmployeeNo);
    }

    [Fact]
    public async Task User_list_orders_numeric_employee_no_by_number_then_name()
    {
        await Login();
        var roleId = await CreateRoleId();
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = "2571",
            Name = "排序用户B",
            RoleIds = new[] { roleId }
        });
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = "434",
            Name = "排序用户A",
            RoleIds = new[] { roleId }
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>("/api/users?keyword=排序用户&pageSize=20");

        list!.Data!.Items.Select(x => x.EmployeeNo).Should().Equal("434", "2571");
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
        login.Data.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task User_import_template_can_be_downloaded()
    {
        await Login();

        var response = await _client.GetAsync("/api/users/import/template");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task User_import_template_preview_is_valid()
    {
        await Login();
        var response = await _client.GetAsync("/api/users/import/template");
        var file = await response.Content.ReadAsByteArrayAsync();

        var preview = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import/validate", file);

        preview.Code.Should().Be(0);
        preview.Data!.Rows.Should().ContainSingle();
        preview.Data.FailedCount.Should().Be(0);
        preview.Data.Rows.Single().RoleName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task User_import_creates_users_by_role_name()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("导入角色"),
            IsActive = true
        });
        var employeeNo1 = Unique("u");
        var employeeNo2 = Unique("u");
        var file = BuildXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "角色名称" },
            new[] { employeeNo1, "导入用户1", $"{employeeNo1}@example.local", role.Data!.Name },
            new[] { employeeNo2, "导入用户2", "", role.Data.Name }
        });

        var imported = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import", file);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword={employeeNo1[..Math.Min(employeeNo1.Length, 12)]}&pageSize=100");
        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = employeeNo1,
            password = "123456"
        });

        imported.Code.Should().Be(0);
        imported.Data!.SuccessCount.Should().Be(2);
        imported.Data.FailedCount.Should().Be(0);
        list!.Data!.Items.Should().Contain(x =>
            x.EmployeeNo == employeeNo1 &&
            x.Name == "导入用户1" &&
            x.Email == $"{employeeNo1}@example.local" &&
            x.RoleNames.Contains(role.Data.Name));
        login.Code.Should().Be(0);
        login.Data!.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task User_import_creates_users_by_department_name()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("导入部门角色"),
            IsActive = true
        });
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = await CreateDepartmentManagerId(role.Data!.Id),
            Name = Unique("导入部门")
        });
        var employeeNo = Unique("u");
        var file = BuildXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "部门名称", "角色名称" },
            new[] { employeeNo, "部门导入用户", $"{employeeNo}@example.local", department.Data!.Name, role.Data.Name }
        });

        var imported = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import", file);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword={employeeNo}");

        imported.Code.Should().Be(0);
        imported.Data!.Rows.Single().DepartmentName.Should().Be(department.Data.Name);
        list!.Data!.Items.Should().ContainSingle(x =>
            x.EmployeeNo == employeeNo &&
            x.DepartmentId == department.Data.Id &&
            x.DepartmentName == department.Data.Name);
    }

    [Fact]
    public async Task User_import_rejects_unknown_department_name()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("导入未知部门角色"),
            IsActive = true
        });
        var employeeNo = Unique("u");
        var file = BuildXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "部门名称", "角色名称" },
            new[] { employeeNo, "未知部门用户", "", "不存在部门", role.Data!.Name }
        });

        var preview = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import/validate", file);

        preview.Code.Should().Be(0);
        preview.Data!.FailedCount.Should().Be(1);
        preview.Data.Rows.Single().Error.Should().Contain("部门名称不存在或已停用");
    }

    [Fact]
    public async Task User_import_validate_previews_rows_without_creating_users()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("导入预览角色"),
            IsActive = true
        });
        var employeeNo = Unique("u");
        var file = BuildXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "角色名称" },
            new[] { employeeNo, "预览用户", $"{employeeNo}@example.local", role.Data!.Name }
        });

        var preview = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import/validate", file);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword={employeeNo}");

        preview.Code.Should().Be(0);
        preview.Data!.SuccessCount.Should().Be(1);
        preview.Data.FailedCount.Should().Be(0);
        preview.Data.Rows.Should().ContainSingle(x =>
            x.EmployeeNo == employeeNo &&
            x.Name == "预览用户" &&
            x.RoleName == role.Data.Name &&
            x.IsValid);
        list!.Data!.Items.Should().NotContain(x => x.EmployeeNo == employeeNo);
    }

    [Fact]
    public async Task User_import_validate_reads_excel_shared_strings()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("共享字符串角色"),
            IsActive = true
        });
        var employeeNo = Unique("u");
        var file = BuildSharedStringXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "角色名称" },
            new[] { employeeNo, "共享字符串用户", $"{employeeNo}@example.local", role.Data!.Name }
        });

        var preview = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import/validate", file);

        preview.Code.Should().Be(0);
        preview.Data!.FailedCount.Should().Be(0);
        preview.Data.Rows.Should().ContainSingle(x =>
            x.EmployeeNo == employeeNo &&
            x.Name == "共享字符串用户" &&
            x.Email == $"{employeeNo}@example.local" &&
            x.RoleName == role.Data.Name &&
            x.IsValid);
    }

    [Fact]
    public async Task User_import_validate_keeps_role_name_when_optional_email_cell_is_blank()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("空邮箱角色"),
            IsActive = true
        });
        var employeeNo = Unique("u");
        var file = BuildSharedStringXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "角色名称" },
            new[] { employeeNo, "空邮箱用户", "", role.Data!.Name }
        });

        var preview = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import/validate", file);

        preview.Code.Should().Be(0);
        preview.Data!.FailedCount.Should().Be(0);
        preview.Data.Rows.Should().ContainSingle(x =>
            x.EmployeeNo == employeeNo &&
            x.Name == "空邮箱用户" &&
            x.Email == null &&
            x.RoleName == role.Data.Name &&
            x.IsValid);
    }

    [Fact]
    public async Task User_import_rejects_invalid_rows_and_does_not_partially_import()
    {
        await Login();
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("批量导入角色"),
            IsActive = true
        });
        var validEmployeeNo = Unique("u");
        var invalidEmployeeNo = Unique("u");
        var file = BuildXlsx(new[]
        {
            new[] { "工号", "姓名", "邮箱", "角色名称" },
            new[] { validEmployeeNo, "有效用户", "", role.Data!.Name },
            new[] { invalidEmployeeNo, "无效用户", "", "不存在角色" }
        });

        var imported = await PostFile<ApiResult<UserImportResultDto>>("/api/users/import", file);
        var validList = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            $"/api/users?keyword={validEmployeeNo}");

        imported.Code.Should().Be(4001);
        imported.Data!.SuccessCount.Should().Be(0);
        imported.Data.FailedCount.Should().Be(1);
        imported.Data.Rows.Should().Contain(x => x.Row == 3 && !x.IsValid && x.Error.Contains("角色名称不存在"));
        validList!.Data!.Items.Should().NotContain(x => x.EmployeeNo == validEmployeeNo);
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
    public async Task Create_user_rejects_duplicate_employee_no_with_business_message()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "原用户",
            RoleIds = new[] { roleId }
        });

        var duplicated = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "重复用户",
            RoleIds = new[] { roleId }
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("工号已存在");
    }

    [Fact]
    public async Task Create_role_rejects_duplicate_code_with_business_message()
    {
        await Login();
        var code = Unique("role");
        await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = code,
            Name = "原角色",
            IsActive = true
        });

        var duplicated = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = code,
            Name = "重复角色",
            IsActive = true
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("角色编码已存在");
    }

    [Fact]
    public async Task Role_rejects_duplicate_name_with_business_message()
    {
        await Login();
        var name = $"重复角色名称-{Guid.NewGuid():N}";
        await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = name,
            IsActive = true
        });
        var target = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = $"{name}-可更新",
            IsActive = true
        });

        var duplicatedCreate = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = name,
            IsActive = true
        });
        var duplicatedUpdate = await Put<ApiResult<RoleDto>>($"/api/roles/{target.Data!.Id}", new
        {
            name,
            isActive = true
        });

        duplicatedCreate.Code.Should().Be(4094);
        duplicatedCreate.Message.Should().Be("角色名称已存在");
        duplicatedUpdate.Code.Should().Be(4094);
        duplicatedUpdate.Message.Should().Be("角色名称已存在");
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
    public async Task Create_permission_rejects_duplicate_code_with_business_message()
    {
        await Login();
        var code = Unique("asset:archive");
        await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = code,
            Name = "原权限",
            Module = "asset"
        });

        var duplicated = await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = code,
            Name = "重复权限",
            Module = "asset"
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("权限编码已存在");
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
        login.Data.MustChangePassword.Should().BeTrue();
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
    public async Task Delete_user_with_business_references_is_blocked()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "资产保管人",
            RoleIds = new[] { roleId }
        });
        var category = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()
        });
        await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "占用保管人资产",
            CategoryId = category.Data!.Id,
            CustodianId = user.Data!.Id
        });

        var deleted = await Delete<ApiResult<object?>>($"/api/users/{user.Data.Id}");

        deleted.Code.Should().Be(4094);
        deleted.Message.Should().Contain("用户已被资产保管人使用");
    }

    [Fact]
    public async Task Delete_role_with_users_is_blocked()
    {
        await Login();
        var employeeNo = Unique("u");
        var roleId = await CreateRoleId();
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "占用角色用户",
            RoleIds = new[] { roleId }
        });

        var deleted = await Delete<ApiResult<object?>>($"/api/roles/{roleId}");

        deleted.Code.Should().Be(4094);
        deleted.Message.Should().Contain("角色已被用户使用");
    }

    [Fact]
    public async Task Delete_permission_with_role_or_menu_references_is_blocked()
    {
        await Login();
        var permission = await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = Unique("perm:delete"),
            Name = "待保护权限",
            Module = "test"
        });
        var roleId = await CreateRoleId();
        await Put<ApiResult<RoleDto>>($"/api/roles/{roleId}/permissions", new
        {
            permissionIds = new[] { permission.Data!.Id }
        });

        var roleReferenced = await Delete<ApiResult<object?>>($"/api/permissions/{permission.Data.Id}");

        roleReferenced.Code.Should().Be(4094);
        roleReferenced.Message.Should().Contain("权限已被角色使用");

        var menuPermission = await Post<ApiResult<PermissionDto>>("/api/permissions", new PermissionDto
        {
            Code = Unique("perm:menu"),
            Name = "菜单引用权限",
            Module = "test"
        });
        await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            Name = Unique("PermMenu"),
            Title = "权限菜单",
            Path = "/test/permission-menu",
            Component = "/test/index",
            Sort = 120,
            Type = "menu",
            PermissionCode = menuPermission.Data!.Code
        });

        var menuReferenced = await Delete<ApiResult<object?>>($"/api/permissions/{menuPermission.Data.Id}");

        menuReferenced.Code.Should().Be(4094);
        menuReferenced.Message.Should().Contain("权限已被菜单使用");
    }

    [Fact]
    public async Task Delete_menu_with_children_or_role_references_is_blocked()
    {
        await Login();
        var parent = await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            Name = Unique("ParentMenu"),
            Title = "父菜单",
            Path = "/test/parent",
            Component = "BasicLayout",
            Sort = 130,
            Type = "menu"
        });
        await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            ParentId = parent.Data!.Id,
            Name = Unique("ChildMenu"),
            Title = "子菜单",
            Path = "/test/parent/child",
            Component = "/test/child",
            Sort = 131,
            Type = "menu"
        });

        var parentDeleted = await Delete<ApiResult<object?>>($"/api/menus/{parent.Data.Id}");

        parentDeleted.Code.Should().Be(4094);
        parentDeleted.Message.Should().Contain("请先删除子菜单");

        var roleMenu = await Post<ApiResult<MenuDto>>("/api/menus", new MenuDto
        {
            Name = Unique("RoleMenu"),
            Title = "角色菜单",
            Path = "/test/role-menu",
            Component = "/test/role-menu",
            Sort = 140,
            Type = "menu"
        });
        var roleId = await CreateRoleId();
        await Put<ApiResult<RoleDto>>($"/api/roles/{roleId}/menus", new
        {
            menuIds = new[] { roleMenu.Data!.Id }
        });

        var roleReferenced = await Delete<ApiResult<object?>>($"/api/menus/{roleMenu.Data.Id}");

        roleReferenced.Code.Should().Be(4094);
        roleReferenced.Message.Should().Contain("菜单已被角色使用");
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

    [Fact]
    public async Task User_cannot_change_own_role_even_with_user_edit_permission()
    {
        await Login();
        var employeeNo = Unique("self");
        var editorRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("self_editor"),
            Name = Unique("自编辑角色"),
            IsActive = true
        });
        var userEditPermission = (await _client.GetFromJsonAsync<ApiResult<List<PermissionDto>>>("/api/permissions"))!
            .Data!
            .Single(x => x.Code == "user:edit");
        await Put<ApiResult<RoleDto>>($"/api/roles/{editorRole.Data!.Id}/permissions", new
        {
            permissionIds = new[] { userEditPermission.Id }
        });
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "自改角色用户",
            Password = "123456",
            RoleIds = new[] { editorRole.Data.Id }
        });
        var adminRole = (await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?pageSize=100"))!
            .Data!
            .Items
            .Single(x => x.Code == "admin");

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data!.Token);
        var updated = await Put<ApiResult<UserDto>>($"/api/users/{user.Data!.Id}", new UpdateUserRequest
        {
            Name = "自改角色用户",
            RoleIds = new[] { adminRole.Id }
        });

        updated.Code.Should().Be(4094);
        updated.Message.Should().Be("不能修改自己的角色");
        await Login();
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={employeeNo}");
        list!.Data!.Items.Single().RoleIds.Should().Equal(editorRole.Data.Id);
    }

    [Fact]
    public async Task User_edit_permission_without_assign_role_cannot_change_other_user_role()
    {
        await Login();
        var editorEmployeeNo = Unique("editor");
        var targetEmployeeNo = Unique("target");
        var editorRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("editor"),
            Name = Unique("用户编辑员"),
            IsActive = true
        });
        var normalRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("normal"),
            Name = Unique("普通目标角色"),
            IsActive = true
        });
        var userEditPermission = (await _client.GetFromJsonAsync<ApiResult<List<PermissionDto>>>("/api/permissions"))!
            .Data!
            .Single(x => x.Code == "user:edit");
        await Put<ApiResult<RoleDto>>($"/api/roles/{editorRole.Data!.Id}/permissions", new
        {
            permissionIds = new[] { userEditPermission.Id }
        });
        var editor = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = editorEmployeeNo,
            Name = "用户编辑员",
            Password = "123456",
            RoleIds = new[] { editorRole.Data.Id }
        });
        var target = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = targetEmployeeNo,
            Name = "被改角色用户",
            RoleIds = new[] { normalRole.Data!.Id }
        });
        var adminRole = (await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?pageSize=100"))!
            .Data!
            .Items
            .Single(x => x.Code == "admin");

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = editorEmployeeNo,
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data!.Token);
        var updated = await Put<ApiResult<UserDto>>($"/api/users/{target.Data!.Id}", new UpdateUserRequest
        {
            Name = "被改角色用户",
            RoleIds = new[] { adminRole.Id }
        });

        updated.Code.Should().Be(4031);
        updated.Message.Should().Be("没有分配用户角色权限");
        await Login();
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={targetEmployeeNo}");
        list!.Data!.Items.Single().RoleIds.Should().Equal(normalRole.Data.Id);
        editor.Data!.RoleIds.Should().Equal(editorRole.Data.Id);
    }

    [Fact]
    public async Task User_edit_permission_without_assign_role_can_update_basic_profile_when_role_unchanged()
    {
        await Login();
        var editorEmployeeNo = Unique("editor");
        var targetEmployeeNo = Unique("target");
        var editorRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("editor"),
            Name = Unique("资料编辑员"),
            IsActive = true
        });
        var normalRole = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("normal"),
            Name = Unique("资料目标角色"),
            IsActive = true
        });
        var userEditPermission = (await _client.GetFromJsonAsync<ApiResult<List<PermissionDto>>>("/api/permissions"))!
            .Data!
            .Single(x => x.Code == "user:edit");
        await Put<ApiResult<RoleDto>>($"/api/roles/{editorRole.Data!.Id}/permissions", new
        {
            permissionIds = new[] { userEditPermission.Id }
        });
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = editorEmployeeNo,
            Name = "资料编辑员",
            Password = "123456",
            RoleIds = new[] { editorRole.Data.Id }
        });
        var target = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = targetEmployeeNo,
            Name = "待改资料用户",
            RoleIds = new[] { normalRole.Data!.Id }
        });

        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = editorEmployeeNo,
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data!.Token);
        var updated = await Put<ApiResult<UserDto>>($"/api/users/{target.Data!.Id}", new UpdateUserRequest
        {
            Name = "已改资料用户",
            RoleIds = new[] { normalRole.Data.Id }
        });

        updated.Code.Should().Be(0);
        updated.Data!.Name.Should().Be("已改资料用户");
        updated.Data.RoleIds.Should().Equal(normalRole.Data.Id);
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

    private async Task<T> PostFile<T>(string url, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "users.xlsx");
        var res = await _client.PostAsync(url, form);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}";

    private async Task<int> CreateRoleId()
    {
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("测试角色"),
            IsActive = true
        });
        return role.Data!.Id;
    }

    private async Task<int> CreateDepartmentManagerId(int roleId)
    {
        var manager = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("mgr"),
            Name = "部门负责人",
            RoleIds = new[] { roleId }
        });
        return manager.Data!.Id;
    }

    private static byte[] BuildXlsx(IEnumerable<string[]> rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Users" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/worksheets/sheet1.xml", BuildSheetXml(rows));
        }

        return ms.ToArray();
    }

    private static byte[] BuildSharedStringXlsx(IEnumerable<string[]> sourceRows)
    {
        var rows = sourceRows.Select(row => row.ToArray()).ToArray();
        var sharedStrings = rows.SelectMany(row => row).Distinct().ToArray();
        var sharedStringIndexes = sharedStrings
            .Select((value, index) => new { value, index })
            .ToDictionary(x => x.value, x => x.index);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Users" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/sharedStrings.xml", BuildSharedStringsXml(sharedStrings));
            WriteEntry(zip, "xl/worksheets/sheet1.xml", BuildSharedStringSheetXml(rows, sharedStringIndexes));
        }

        return ms.ToArray();
    }

    private static string BuildSheetXml(IEnumerable<string[]> rows)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetRows = rows.Select((cells, rowIndex) =>
            $"""<row r="{rowIndex + 1}">{string.Concat(cells.Select((cell, colIndex) => $"""<c r="{ColumnName(colIndex + 1)}{rowIndex + 1}" t="inlineStr"><is><t>{System.Security.SecurityElement.Escape(cell)}</t></is></c>"""))}</row>""");
        return $"""<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="{ns}"><sheetData>{string.Concat(sheetRows)}</sheetData></worksheet>""";
    }

    private static string BuildSharedStringsXml(string[] values)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var items = string.Concat(values.Select(value => $"""<si><t>{System.Security.SecurityElement.Escape(value)}</t></si>"""));
        return $"""<?xml version="1.0" encoding="UTF-8"?><sst xmlns="{ns}" count="{values.Length}" uniqueCount="{values.Length}">{items}</sst>""";
    }

    private static string BuildSharedStringSheetXml(string[][] rows, IReadOnlyDictionary<string, int> sharedStringIndexes)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetRows = rows.Select((cells, rowIndex) =>
            $"""<row r="{rowIndex + 1}">{string.Concat(cells.Select((cell, colIndex) => new { cell, colIndex }).Where(x => x.cell != "").Select(x => $"""<c r="{ColumnName(x.colIndex + 1)}{rowIndex + 1}" t="s"><v>{sharedStringIndexes[x.cell]}</v></c>"""))}</row>""");
        return $"""<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="{ns}"><sheetData>{string.Concat(sheetRows)}</sheetData></worksheet>""";
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ColumnName(int index)
    {
        var name = "";
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }

        return name;
    }
}
