using AssetManagement.Domain.Entities;

namespace AssetManagement.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any())
        {
            return;
        }

        var permissions = new[]
        {
            new Permission { Code = "asset:view", Name = "查看资产", Module = "asset" },
            new Permission { Code = "asset:create", Name = "新增资产", Module = "asset" },
            new Permission { Code = "asset:edit", Name = "编辑资产", Module = "asset" },
            new Permission { Code = "asset:delete", Name = "删除资产", Module = "asset" },
            new Permission { Code = "approval:handle", Name = "处理审批", Module = "approval" },
            new Permission { Code = "approval:view", Name = "查看审批", Module = "approval" },
            new Permission { Code = "report:view", Name = "查看报表", Module = "report" },
            new Permission { Code = "admin:user", Name = "用户管理", Module = "admin" },
            new Permission { Code = "admin:role", Name = "角色管理", Module = "admin" },
            new Permission { Code = "admin:audit", Name = "审计日志", Module = "admin" },
            new Permission { Code = "admin:setting", Name = "系统参数", Module = "admin" },
            new Permission { Code = "workflow:design", Name = "流程设计", Module = "workflow" }
        };

        db.Permissions.AddRange(permissions);

        var menus = new[]
        {
            new Menu { Id = 1, Name = "Asset", Title = "资产管理", Path = "/asset", Component = "BasicLayout", Icon = "lucide:package", Sort = 10 },
            new Menu { Id = 2, ParentId = 1, Name = "AssetList", Title = "资产列表", Path = "/asset/list", Component = "/asset/list/index", Sort = 11, PermissionCode = "asset:view" },
            new Menu { Id = 3, ParentId = 1, Name = "AssetHierarchy", Title = "资产层级", Path = "/asset/hierarchy", Component = "/asset/hierarchy/index", Sort = 12, PermissionCode = "asset:view" },
            new Menu { Id = 18, ParentId = 1, Name = "AssetCategories", Title = "资产分类", Path = "/asset/categories", Component = "/asset/categories/index", Sort = 13, PermissionCode = "asset:view" },
            new Menu { Id = 19, ParentId = 1, Name = "AssetLocations", Title = "存放位置", Path = "/asset/locations", Component = "/asset/locations/index", Sort = 14, PermissionCode = "asset:view" },
            new Menu { Id = 4, Name = "Approval", Title = "审批管理", Path = "/approval", Component = "BasicLayout", Icon = "lucide:git-branch", Sort = 20 },
            new Menu { Id = 5, ParentId = 4, Name = "ApprovalPending", Title = "待我审批", Path = "/approval/pending", Component = "/approval/pending/index", Sort = 21, PermissionCode = "approval:handle" },
            new Menu { Id = 6, ParentId = 4, Name = "ApprovalMine", Title = "我的申请", Path = "/approval/mine", Component = "/approval/mine/index", Sort = 22, PermissionCode = "approval:view" },
            new Menu { Id = 7, Name = "Report", Title = "报表统计", Path = "/report", Component = "BasicLayout", Icon = "lucide:chart-column", Sort = 30 },
            new Menu { Id = 8, ParentId = 7, Name = "ReportSummary", Title = "资产汇总", Path = "/report/summary", Component = "/report/summary/index", Sort = 31, PermissionCode = "report:view" },
            new Menu { Id = 9, ParentId = 7, Name = "ReportBorrow", Title = "借用明细", Path = "/report/borrow", Component = "/report/borrow/index", Sort = 32, PermissionCode = "report:view" },
            new Menu { Id = 10, Name = "Admin", Title = "系统管理", Path = "/admin", Component = "BasicLayout", Icon = "lucide:settings", Sort = 40 },
            new Menu { Id = 11, ParentId = 10, Name = "AdminUsers", Title = "用户管理", Path = "/admin/users", Component = "/admin/users/index", Sort = 41, PermissionCode = "admin:user" },
            new Menu { Id = 12, ParentId = 10, Name = "AdminRoles", Title = "角色管理", Path = "/admin/roles", Component = "/admin/roles/index", Sort = 42, PermissionCode = "admin:role" },
            new Menu { Id = 13, ParentId = 10, Name = "AdminWorkflows", Title = "审批流程", Path = "/admin/workflows", Component = "/admin/workflows/index", Sort = 43, PermissionCode = "workflow:design" },
            new Menu { Id = 14, ParentId = 10, Name = "AdminAudit", Title = "审计日志", Path = "/admin/audit", Component = "/admin/audit/index", Sort = 44, PermissionCode = "admin:audit" },
            new Menu { Id = 20, ParentId = 10, Name = "AdminDepartments", Title = "组织架构", Path = "/admin/departments", Component = "/admin/departments/index", Sort = 45, PermissionCode = "admin:user" },
            new Menu { Id = 21, ParentId = 10, Name = "AdminSettings", Title = "系统参数", Path = "/admin/settings", Component = "/admin/settings/index", Sort = 46, PermissionCode = "admin:setting" },
            new Menu { Id = 15, ParentId = 2, Name = "AssetCreateButton", Title = "新增资产按钮", Type = "button", Sort = 1, PermissionCode = "asset:create" },
            new Menu { Id = 16, ParentId = 2, Name = "AssetEditButton", Title = "编辑资产按钮", Type = "button", Sort = 2, PermissionCode = "asset:edit" },
            new Menu { Id = 17, ParentId = 2, Name = "AssetDeleteButton", Title = "删除资产按钮", Type = "button", Sort = 3, PermissionCode = "asset:delete" }
        };

        db.Menus.AddRange(menus);

        var roles = new[]
        {
            new Role { Code = "admin", Name = "系统管理员" },
            new Role { Code = "warehouse", Name = "仓库管理员" },
            new Role { Code = "supervisor", Name = "部门主管" },
            new Role { Code = "employee", Name = "普通员工" },
            new Role { Code = "dept_admin", Name = "部门管理员" }
        };

        db.Roles.AddRange(roles);
        db.SaveChanges();

        var adminRole = db.Roles.Single(x => x.Code == "admin");
        db.RolePermissions.AddRange(db.Permissions.Select(x => new RolePermission
        {
            RoleId = adminRole.Id,
            PermissionId = x.Id
        }));
        db.RoleMenus.AddRange(db.Menus.Select(x => new RoleMenu
        {
            RoleId = adminRole.Id,
            MenuId = x.Id
        }));

        var admin = new User
        {
            EmployeeNo = "1001",
            Name = "系统管理员",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true
        };
        db.Users.Add(admin);
        db.SaveChanges();

        db.UserRoles.Add(new UserRole
        {
            UserId = admin.Id,
            RoleId = adminRole.Id
        });
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "audit_retention_months", Value = "12", Description = "审计日志保留月数" },
            new SystemSetting { Key = "attachment_max_mb", Value = "20", Description = "附件大小限制 MB" },
            new SystemSetting { Key = "page_size", Value = "20", Description = "默认每页记录数" }
        );
        db.SaveChanges();
    }
}
