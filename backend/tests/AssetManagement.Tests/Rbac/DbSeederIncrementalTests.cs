using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence.Seed;
using AssetManagement.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Tests.Rbac;

public class DbSeederIncrementalTests : MySqlFixtureBase
{
    [Fact]
    public void Historical_workflow_name_respects_database_length_at_boundary()
    {
        var workflow = new WorkflowEntity { Id = int.MaxValue, Name = new string('长', 100) };

        var name = DbSeeder.HistoricalWorkflowName(workflow);

        name.Should().HaveLength(100);
        name.Should().EndWith($"（历史版本 {int.MaxValue}）");
    }

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
    public void Incremental_seed_keeps_three_roles_and_migrates_legacy_role_users_to_supervisor()
    {
        SeedLegacyDatabaseState();

        DbSeeder.Seed(_db);

        var roles = _db.Roles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Include(x => x.RoleMenus)
            .ThenInclude(x => x.Menu)
            .ToList();
        var permissions = _db.Permissions.OrderBy(x => x.Code).ToList();
        var admin = roles.Single(x => x.Code == "admin");
        var supervisor = roles.Single(x => x.Code == "supervisor");
        var legacyUser = _db.Users
            .Include(x => x.UserRoles)
            .Single(x => x.EmployeeNo == "9104");
        var normalUser = _db.Users
            .Include(x => x.UserRoles)
            .Single(x => x.EmployeeNo == "9105");

        roles.Select(x => x.Code).Should().BeEquivalentTo("admin", "supervisor", "employee");
        admin.RolePermissions.Select(x => x.Permission.Code)
            .Should().Contain(new[] { "asset:view", "project:view", "material:view", "backup:manage" },
                "增量种子必须保留已有授权并为管理员逐项补齐后来新增的权限");
        admin.RoleMenus.Select(x => x.MenuId).Distinct()
            .Should().Contain(homeMenuId, "已有菜单授权不能丢失");
        admin.RoleMenus.Select(x => x.Menu.Name)
            .Should().Contain(new[] { "Material", "MaterialHome", "MaterialProjects", "AdminBackups" },
                "管理员已有任意菜单时也必须补齐新增模块入口");
        supervisor.RoleMenus.Select(x => x.MenuId).Distinct().Should().NotBeEmpty();
        supervisor.RolePermissions.Select(x => x.Permission.Code)
            .Should().Contain("project:view", "部门主管已有测试项目菜单时必须能访问测试项目接口");
        supervisor.RolePermissions.Select(x => x.Permission.Code)
            .Should().NotContain("user:create", "部门主管只能查看部门人员，不能新建用户");
        roles.Single(x => x.Code == "employee").RoleMenus.Select(x => x.Menu.Name)
            .Should().Contain(new[] { "Material", "MaterialHome", "MaterialProjects" },
                "普通员工已有菜单时仍应增量补齐新产品模块入口");
        legacyUser.UserRoles.Should()
            .ContainSingle(x => x.RoleId == supervisor.Id, "旧仓库/部门管理员应合并为部门主管");
        normalUser.UserRoles.Select(x => x.RoleId)
            .Should().NotContain(supervisor.Id, "普通员工不应被误升级为部门主管");

        permissions.Select(x => x.Code).Should().Contain(new[]
        {
            "category:view", "category:create", "category:edit", "category:delete", "category:restore", "category:purge",
            "location:view", "location:create", "location:edit", "location:delete",
            "asset:import", "asset:export",
            "file:upload", "file:view",
            "approval:add-sign", "approval:transfer-sign", "approval:confirm-return",
            "report:export", "report:remind",
            "user:view", "user:create", "user:edit", "user:assign-role", "user:delete", "user:reset-password", "user:toggle-status",
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
    public void Repeated_seed_preserves_custom_supervisor_permissions_and_menus_after_defaults_initialized()
    {
        SeedLegacyDatabaseState();
        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        var supervisor = _db.Roles.Single(x => x.Code == "supervisor");
        var assetCreate = _db.Permissions.Single(x => x.Code == "asset:create");
        var userCreate = _db.Permissions.Single(x => x.Code == "user:create");
        var materialHome = _db.Menus.Single(x => x.Name == "MaterialHome");
        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x =>
            x.RoleId == supervisor.Id && x.PermissionId == assetCreate.Id));
        _db.RolePermissions.Add(new RolePermission
        {
            RoleId = supervisor.Id,
            PermissionId = userCreate.Id
        });
        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x =>
            x.RoleId == supervisor.Id && x.MenuId == materialHome.Id));
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        var reloaded = _db.Roles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Include(x => x.RoleMenus)
            .ThenInclude(x => x.Menu)
            .Single(x => x.Code == "supervisor");
        reloaded.RolePermissions.Select(x => x.Permission.Code).Should().NotContain("asset:create");
        reloaded.RolePermissions.Select(x => x.Permission.Code).Should().Contain("user:create");
        reloaded.RoleMenus.Select(x => x.Menu.Name).Should().NotContain("MaterialHome");
        _db.SystemSettings.Should().Contain(x => x.Key == "rbac_core_role_defaults_initialized_v1");
    }

    [Fact]
    public void Incremental_seed_grants_employee_file_upload_once_and_then_preserves_customization()
    {
        SeedLegacyDatabaseState();
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "rbac_core_role_defaults_initialized_v1",
            Value = "true",
            Description = "模拟已完成旧版基础角色初始化"
        });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        var employee = _db.Roles.Single(x => x.Code == "employee");
        var fileUpload = _db.Permissions.Single(x => x.Code == "file:upload");
        _db.RolePermissions.Should().Contain(x =>
            x.RoleId == employee.Id && x.PermissionId == fileUpload.Id,
            "已有数据库升级后普通员工也必须能上传测试料件图片");
        _db.SystemSettings.Should().Contain(x =>
            x.Key == "rbac_employee_file_upload_initialized_v1");

        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x =>
            x.RoleId == employee.Id && x.PermissionId == fileUpload.Id));
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        _db.RolePermissions.Should().NotContain(x =>
            x.RoleId == employee.Id && x.PermissionId == fileUpload.Id,
            "一次性升级完成后应继续保留管理员对角色授权的后续调整");
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

    [Fact]
    public void Incremental_seed_removes_legacy_material_flow_permissions_and_migrates_role_grants()
    {
        SeedLegacyDatabaseState();
        var admin = _db.Roles.Single(x => x.Code == "admin");
        var supervisor = _db.Roles.Single(x => x.Code == "supervisor");
        var legacyTransfer = new Permission { Code = "material:transfer", Name = "发起料件流转", Module = "material" };
        var legacyApprove = new Permission { Code = "material:approve", Name = "审批料件流转", Module = "material" };
        var legacyAdminUser = new Permission { Code = "admin:user", Name = "用户管理", Module = "admin" };
        _db.Permissions.AddRange(legacyTransfer, legacyApprove, legacyAdminUser);
        _db.SaveChanges();
        _db.RolePermissions.AddRange(
            new RolePermission { RoleId = admin.Id, PermissionId = legacyAdminUser.Id },
            new RolePermission { RoleId = supervisor.Id, PermissionId = legacyTransfer.Id },
            new RolePermission { RoleId = supervisor.Id, PermissionId = legacyApprove.Id });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        _db.Permissions.Select(x => x.Code)
            .Should().NotContain(new[] { "material:transfer", "material:approve", "admin:user" });
        _db.Roles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Single(x => x.Code == "admin")
            .RolePermissions.Select(x => x.Permission.Code)
            .Should().Contain("user:view");
        _db.Roles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Single(x => x.Code == "supervisor")
            .RolePermissions.Select(x => x.Permission.Code)
            .Should().Contain(new[] { "material-flow:transfer", "material-flow:approve" });
    }

    [Fact]
    public void Incremental_seed_uses_stable_user_and_role_references_in_default_workflows()
    {
        SeedLegacyDatabaseState();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        var adminId = _db.Users.Single(x => x.EmployeeNo == "1001").Id;
        _db.Workflows.Single(x => x.BizType == "transfer").BpmnXml
            .Should().Contain("camunda:candidateGroups=\"role:supervisor\"");
        _db.Workflows.Single(x => x.BizType == "return").BpmnXml
            .Should().Contain("camunda:candidateGroups=\"role:supervisor\"");
        var materialXml = _db.Workflows.Single(x => x.BizType == "material_transfer").BpmnXml;
        materialXml.Should().Contain($"camunda:assignee=\"user:{adminId}\"");
        materialXml.Should().NotContain("camunda:assignee=\"1001\"");
    }

    [Fact]
    public void Incremental_seed_replenishes_missing_default_workflow_without_overwriting_custom_workflows()
    {
        SeedLegacyDatabaseState();
        DbSeeder.Seed(_db);
        var custom = new WorkflowEntity { Name = "自定义流程", BizType = "custom_seed", BpmnXml = "<custom />", IsActive = false };
        _db.Workflows.Add(custom);
        _db.Workflows.Remove(_db.Workflows.Single(x => x.BizType == "return"));
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        _db.Workflows.Should().ContainSingle(x => x.BizType == "return");
        _db.Workflows.Single(x => x.BizType == "custom_seed").BpmnXml.Should().Be("<custom />");
    }

    [Fact]
    public void Repeated_seed_supports_multiple_material_workflow_versions()
    {
        SeedLegacyDatabaseState();
        DbSeeder.Seed(_db);
        var active = _db.Workflows.Single(x => x.BizType == "material_transfer" && x.IsActive);
        _db.Workflows.Add(new WorkflowEntity
        {
            Name = "料件流转历史版本",
            BizType = "material_transfer",
            BpmnXml = active.BpmnXml,
            IsActive = false
        });
        _db.SaveChanges();

        var action = () => DbSeeder.Seed(_db);

        action.Should().NotThrow();
        _db.Workflows.Count(x => x.BizType == "material_transfer").Should().Be(2);
        _db.Workflows.Should().ContainSingle(x => x.BizType == "material_transfer" && x.IsActive);
    }

    [Fact]
    public void Incremental_seed_does_not_rewrite_definition_used_by_pending_instance()
    {
        SeedLegacyDatabaseState();
        var xml = """
                  <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
                    <bpmn:process id="custom"><bpmn:userTask id="Task" camunda:candidateGroups="warehouse" /></bpmn:process>
                  </bpmn:definitions>
                  """;
        var workflow = new WorkflowEntity { Name = "待办历史流程", BizType = "pending_seed", BpmnXml = xml, IsActive = false };
        var category = new AssetCategory { CodeSeg = "SEED", Code = "SEED" };
        _db.Workflows.Add(workflow);
        _db.AssetCategories.Add(category);
        _db.SaveChanges();
        var asset = new Asset
        {
            AssetNo = "SEED-ASSET",
            Name = "种子测试资产",
            CategoryId = category.Id,
            Status = AssetStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        _db.SaveChanges();
        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "SEED-PENDING-001",
            BizType = workflow.BizType,
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = _db.Users.First().Id,
            Applicant = "申请人",
            Status = "pending",
            ActiveScopeKey = $"asset:{asset.Id}",
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        _db.Workflows.Single(x => x.Id == workflow.Id).BpmnXml.Should().Be(xml);
    }

    [Fact]
    public void Incremental_seed_does_not_fill_blank_borrow_definition_used_by_pending_instance()
    {
        SeedLegacyDatabaseState();
        DbSeeder.Seed(_db);
        var workflow = _db.Workflows.Single(x => x.BizType == "borrow" && x.IsActive);
        workflow.BpmnXml = null;
        var category = new AssetCategory { CodeSeg = "BRW", Code = $"BRW-{Guid.NewGuid():N}" };
        _db.AssetCategories.Add(category);
        _db.SaveChanges();
        var asset = new Asset
        {
            AssetNo = $"BRW-{Guid.NewGuid():N}", Name = "空定义借用资产", CategoryId = category.Id,
            Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        _db.SaveChanges();
        var applicant = _db.Users.First();
        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = $"BRW-{Guid.NewGuid():N}", BizType = "borrow", WorkflowId = workflow.Id,
            AssetId = asset.Id, AssetNo = asset.AssetNo, AssetName = asset.Name,
            ApplicantId = applicant.Id, Applicant = applicant.Name, Status = "pending",
            ActiveScopeKey = $"asset:{asset.Id}", ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        _db.SaveChanges();

        DbSeeder.Seed(_db);

        _db.ChangeTracker.Clear();
        _db.Workflows.Single(x => x.Id == workflow.Id).BpmnXml.Should().BeNull();
    }

    private void SeedLegacyDatabaseState()
    {
        var admin = new Role { Code = "admin", Name = "系统管理员" };
        var supervisor = new Role { Code = "supervisor", Name = "部门主管" };
        var employee = new Role { Code = "employee", Name = "普通员工" };
        var warehouse = new Role { Code = "warehouse", Name = "仓库管理员" };
        var deptAdmin = new Role { Code = "dept_admin", Name = "部门管理员" };
        _db.Roles.AddRange(admin, supervisor, employee, warehouse, deptAdmin);

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
        _db.RolePermissions.Add(new RolePermission
        {
            RoleId = supervisor.Id,
            PermissionId = permissions.Single(x => x.Code == "approval:create").Id
        });
        _db.RoleMenus.Add(new RoleMenu { RoleId = admin.Id, MenuId = home.Id });
        _db.UserRoles.Add(new UserRole { UserId = systemAdmin.Id, RoleId = admin.Id });
        _db.UserRoles.Add(new UserRole { UserId = warehouseUser.Id, RoleId = warehouse.Id });
        _db.UserRoles.Add(new UserRole { UserId = normalUser.Id, RoleId = employee.Id });
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
