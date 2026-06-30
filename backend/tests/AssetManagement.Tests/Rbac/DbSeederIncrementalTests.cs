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
    public void Incremental_seed_repairs_roles_admin_grants_and_warehouse_user()
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
            .Should().BeEquivalentTo(permissions.Select(x => x.Code), "系统管理员应始终拥有全部权限，包含后续增量新增的权限");
        admin.RoleMenus.Select(x => x.MenuId).Distinct()
            .Should().HaveCount(_db.Menus.Count(), "系统管理员应始终拥有全部菜单，避免新增菜单后没有入口");
        warehouse!.RoleMenus.Select(x => x.MenuId).Distinct()
            .Should().NotBeEmpty("仓库管理员恢复后也应按权限矩阵补齐菜单入口");
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
