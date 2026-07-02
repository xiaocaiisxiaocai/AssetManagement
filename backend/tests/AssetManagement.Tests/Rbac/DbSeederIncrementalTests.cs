using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence.Seed;
using AssetManagement.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Rbac;

public class DbSeederIncrementalTests : MySqlFixtureBase
{
    [Fact]
    public void Seed_contains_every_controller_permission_code()
    {
        SeedLegacyDatabaseState();

        DbSeeder.Seed(_db);

        var dbCodes = _db.Permissions.Select(x => x.Code).ToHashSet();
        var root = FindRepositoryRoot();
        var controllerCodes = Directory
            .EnumerateFiles(Path.Combine(root, "backend", "src", "AssetManagement.Api", "Controllers"), "*.cs")
            .SelectMany(File.ReadLines)
            .Select(line => System.Text.RegularExpressions.Regex.Match(line, "HasPermission\\(\"([^\"]+)\"\\)"))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        controllerCodes.Should().OnlyContain(code => dbCodes.Contains(code), "所有接口权限码必须能由种子补齐");
    }

    [Fact]
    public void Incremental_seed_repairs_roles_menus_and_warehouse_user_without_overwriting_existing_grants()
    {
        SeedLegacyDatabaseState();

        DbSeeder.Seed(_db);

        var roles = _db.Roles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Include(x => x.RoleMenus)
            .ToList();
        var permissions = _db.Permissions.OrderBy(x => x.Code).ToList();
        var admin = roles.Single(x => x.Code == "admin");
        var warehouse = roles.SingleOrDefault(x => x.Code == "warehouse");
        var warehouseUser = _db.Users
            .Include(x => x.UserRoles)
            .Single(x => x.EmployeeNo == "9104");
        var normalUser = _db.Users
            .Include(x => x.UserRoles)
            .Single(x => x.EmployeeNo == "9105");

        warehouse.Should().NotBeNull();
        admin.RolePermissions.Select(x => x.Permission.Code)
            .Should().BeEquivalentTo(new[] { "asset:view" }, "角色管理中已存在的授权关系不能在启动种子时被覆盖");
        admin.RoleMenus.Select(x => x.MenuId).Distinct()
            .Should().BeEquivalentTo(new[] { homeMenuId }, "角色管理中已存在的菜单授权不能在启动种子时被覆盖");
        warehouse!.RoleMenus.Select(x => x.MenuId).Distinct()
            .Should().NotBeEmpty("仓库管理员恢复后也应按权限矩阵补齐菜单入口");
        warehouse.RolePermissions.Select(x => x.Permission.Code)
            .Should().Contain("project:view", "仓库管理员已有测试项目菜单时必须能访问测试项目接口");
        roles.Single(x => x.Code == "supervisor")
            .RolePermissions.Select(x => x.Permission.Code)
            .Should().Contain("project:view", "部门主管已有测试项目菜单时必须能访问测试项目接口");
        warehouseUser.UserRoles.Select(x => x.RoleId)
            .Should().Contain(warehouse!.Id, "现有仓库管理员测试用户不能保持无角色状态");
        normalUser.UserRoles.Select(x => x.RoleId)
            .Should().NotContain(warehouse.Id, "不能只按历史工号误把普通用户绑定为仓库管理员");

        permissions.Select(x => x.Code).Should().Contain(new[]
        {
            "category:view", "category:create", "category:edit", "category:delete", "category:restore", "category:purge",
            "location:view", "location:create", "location:edit", "location:delete",
            "asset:import", "asset:export",
            "file:upload", "file:view",
            "approval:add-sign", "approval:transfer-sign", "approval:confirm-return",
            "report:export", "report:remind",
            "user:view", "user:create", "user:edit", "user:delete", "user:reset-password", "user:toggle-status",
            "department:view", "department:create", "department:edit", "department:delete",
            "role:view", "role:create", "role:edit", "role:delete", "role:assign-permission", "role:assign-menu",
            "permission:manage", "menu:manage",
            "workflow:view", "workflow:create", "workflow:edit", "workflow:delete", "workflow:design",
            "project:view", "project:create", "project:edit", "project:delete", "project:restore", "project:purge", "project:option", "project:followup",
            "material:return",
            "material-flow:view", "material-flow:transfer", "material-flow:approve"
        });
    }

    [Fact]
    public void Incremental_seed_repairs_admin_menu_order_for_existing_database()
    {
        SeedLegacyDatabaseState();
        var adminMenu = new Menu { Name = "Admin", Title = "系统管理", Path = "/admin", Component = "BasicLayout", Sort = 40 };
        _db.Menus.Add(adminMenu);
        _db.SaveChanges();
        _db.Menus.AddRange(
            new Menu { ParentId = adminMenu.Id, Name = "AdminAudit", Title = "审计日志", Path = "/admin/audit", Component = "/admin/audit/index", Sort = 41 },
            new Menu { ParentId = adminMenu.Id, Name = "AdminDepartments", Title = "组织架构", Path = "/admin/departments", Component = "/admin/departments/index", Sort = 42 },
            new Menu { ParentId = adminMenu.Id, Name = "AdminRoles", Title = "角色管理", Path = "/admin/roles", Component = "/admin/roles/index", Sort = 43 },
            new Menu { ParentId = adminMenu.Id, Name = "AdminUsers", Title = "用户管理", Path = "/admin/users", Component = "/admin/users/index", Sort = 44 },
            new Menu { ParentId = adminMenu.Id, Name = "AdminSettings", Title = "系统参数", Path = "/admin/settings", Component = "/admin/settings/index", Sort = 45 },
            new Menu { ParentId = adminMenu.Id, Name = "AdminBackups", Title = "数据库备份", Path = "/admin/backups", Component = "/admin/backups/index", Sort = 46 },
            new Menu { ParentId = adminMenu.Id, Name = "AdminWorkflows", Title = "审批流程", Path = "/admin/workflows", Component = "/admin/workflows/index", Sort = 47 }
        );
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.Menus
            .Where(x => x.ParentId == adminMenu.Id)
            .OrderBy(x => x.Sort)
            .Select(x => x.Name)
            .ToList()
            .Should().Equal(
                "AdminUsers",
                "AdminRoles",
                "AdminDepartments",
                "AdminWorkflows",
                "AdminSettings",
                "AdminAudit",
                "AdminBackups");
    }

    private void SeedLegacyDatabaseState()
    {
        var admin = new Role { Code = "admin", Name = "系统管理员" };
        var supervisor = new Role { Code = "supervisor", Name = "部门主管" };
        var employee = new Role { Code = "employee", Name = "普通员工" };
        var deptAdmin = new Role { Code = "dept_admin", Name = "部门管理员" };
        _db.Roles.AddRange(admin, supervisor, employee, deptAdmin);

        var permissions = new[]
        {
            new Permission { Code = "asset:view", Name = "查看资产", Module = "asset" },
            new Permission { Code = "approval:create", Name = "发起审批", Module = "approval" },
        };
        _db.Permissions.AddRange(permissions);

        var home = new Menu { Name = "Home", Title = "首页", Path = "/home-root", Component = "BasicLayout", Sort = 1 };
        _db.Menus.Add(home);

        var systemAdmin = new User
        {
            EmployeeNo = "1001",
            Name = "系统管理员",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true
        };
        var warehouseUser = new User
        {
            EmployeeNo = "9104",
            Name = "CODX-Warehouse",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true
        };
        var normalUser = new User
        {
            EmployeeNo = "9105",
            Name = "CODX-MFG-Employee",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true
        };
        _db.Users.AddRange(systemAdmin, warehouseUser, normalUser);
        _db.SaveChanges();

        _db.RolePermissions.Add(new RolePermission
        {
            RoleId = admin.Id,
            PermissionId = permissions.Single(x => x.Code == "asset:view").Id
        });
        _db.RoleMenus.Add(new RoleMenu { RoleId = admin.Id, MenuId = home.Id });
        _db.UserRoles.Add(new UserRole { UserId = systemAdmin.Id, RoleId = admin.Id });
        _db.SaveChanges();
    }

    private int homeMenuId => _db.Menus.Single(x => x.Name == "Home").Id;

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("无法定位仓库根目录");
    }
}
