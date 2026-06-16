using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any())
        {
            SeedIncremental(db);
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
            new Menu { Id = 23, ParentId = 4, Name = "ConfirmReturn", Title = "待确认入库", Path = "/approval/confirm-return", Component = "/approval/confirm-return/index", Sort = 23, PermissionCode = "asset:edit" },
            new Menu { Id = 7, Name = "Report", Title = "报表统计", Path = "/report", Component = "BasicLayout", Icon = "lucide:chart-column", Sort = 30 },
            new Menu { Id = 8, ParentId = 7, Name = "ReportSummary", Title = "资产汇总", Path = "/report/summary", Component = "/report/summary/index", Sort = 31, PermissionCode = "report:view" },
            new Menu { Id = 9, ParentId = 7, Name = "ReportBorrow", Title = "借用明细", Path = "/report/borrow", Component = "/report/borrow/index", Sort = 32, PermissionCode = "report:view" },
            new Menu { Id = 22, ParentId = 7, Name = "ReportOverdue", Title = "逾期资产", Path = "/report/overdue", Component = "/report/overdue/index", Sort = 33, PermissionCode = "report:view" },
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

        // 非 admin 角色的默认权限与菜单（参照需求文档权限矩阵）
        var rolePermissionMap = new Dictionary<string, string[]>
        {
            ["warehouse"] = new[] { "asset:view", "asset:create", "asset:edit", "asset:delete", "approval:handle", "approval:view", "report:view", "admin:audit", "admin:setting", "workflow:design" },
            ["supervisor"] = new[] { "asset:view", "approval:handle", "approval:view", "report:view" },
            ["dept_admin"] = new[] { "asset:view", "asset:create", "asset:edit", "approval:handle", "approval:view", "report:view" },
            ["employee"] = new[] { "asset:view", "approval:view" }
        };
        var allMenusForSeed = db.Menus.ToList();
        foreach (var pair in rolePermissionMap)
        {
            var role = db.Roles.Single(x => x.Code == pair.Key);
            var perms = db.Permissions.Where(p => pair.Value.Contains(p.Code)).ToList();
            db.RolePermissions.AddRange(perms.Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id }));

            // 赋予权限码匹配的菜单 + 其所有祖先菜单（否则 vben 无父路由无法渲染子菜单）
            var menuIds = new HashSet<int>();
            foreach (var menu in allMenusForSeed.Where(m => m.PermissionCode != null && pair.Value.Contains(m.PermissionCode)))
            {
                menuIds.Add(menu.Id);
                var cursor = menu;
                while (cursor.ParentId.HasValue)
                {
                    menuIds.Add(cursor.ParentId.Value);
                    cursor = allMenusForSeed.First(x => x.Id == cursor.ParentId.Value);
                }
            }
            db.RoleMenus.AddRange(menuIds.Select(id => new RoleMenu { RoleId = role.Id, MenuId = id }));
        }

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
        db.Workflows.AddRange(DefaultWorkflows());
        db.SaveChanges();
    }

    private static void SeedIncremental(AppDbContext db)
    {
        if (!db.Workflows.Any())
        {
            db.Workflows.AddRange(DefaultWorkflows());
        }

        if (!db.SystemSettings.Any(x => x.Key == "audit_retention_months"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "audit_retention_months", Value = "12", Description = "审计日志保留月数" });
        }

        if (!db.SystemSettings.Any(x => x.Key == "attachment_max_mb"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "attachment_max_mb", Value = "20", Description = "附件大小限制 MB" });
        }

        if (!db.SystemSettings.Any(x => x.Key == "page_size"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "page_size", Value = "20", Description = "默认每页记录数" });
        }

        if (!db.Menus.Any(x => x.Name == "ReportOverdue"))
        {
            var menu = new Menu { Id = db.Menus.Max(x => x.Id) + 1, ParentId = 7, Name = "ReportOverdue", Title = "逾期资产", Path = "/report/overdue", Component = "/report/overdue/index", Sort = 33, PermissionCode = "report:view" };
            db.Menus.Add(menu);
            var adminRole = db.Roles.SingleOrDefault(x => x.Code == "admin");
            if (adminRole is not null)
            {
                db.RoleMenus.Add(new RoleMenu { RoleId = adminRole.Id, MenuId = menu.Id });
            }
        }

        db.SaveChanges();
    }

    private static WorkflowEntity[] DefaultWorkflows() => new[]
    {
        new WorkflowEntity
        {
            Name = "资产借用流程",
            BizType = "borrow",
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "b1", Name = "发起", Type = NodeType.Start },
                new() { Id = "b2", Name = "直属主管审批", Type = NodeType.Approval, ApproverType = ApproverType.Supervisor, Approver = "李主管" },
                new() { Id = "b3", Name = "资产管理员会签", Type = NodeType.Countersign, ApproverType = ApproverType.Role, Approver = "资产管理员", Signers = new List<string> { "张三", "赵敏" } },
                new() { Id = "b4", Name = "分管副总审批", Type = NodeType.Condition, ApproverType = ApproverType.User, Approver = "王副总", Condition = "amount>5000" },
                new() { Id = "b5", Name = "结束", Type = NodeType.End }
            }
        },
        new WorkflowEntity
        {
            Name = "资产转让流程",
            BizType = "transfer",
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "t1", Name = "发起", Type = NodeType.Start },
                new() { Id = "t2", Name = "直属主管审批", Type = NodeType.Approval, ApproverType = ApproverType.Supervisor, Approver = "李主管" },
                new() { Id = "t3", Name = "接收部门负责人", Type = NodeType.Approval, ApproverType = ApproverType.DeptManager, Approver = "接收部门负责人" },
                new() { Id = "t4", Name = "结束", Type = NodeType.End }
            }
        },
        new WorkflowEntity
        {
            Name = "资产归还流程",
            BizType = "return",
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "r1", Name = "发起", Type = NodeType.Start },
                new() { Id = "r2", Name = "资产管理员确认", Type = NodeType.Approval, ApproverType = ApproverType.Role, Approver = "资产管理员" },
                new() { Id = "r3", Name = "结束", Type = NodeType.End }
            }
        }
    };
}
