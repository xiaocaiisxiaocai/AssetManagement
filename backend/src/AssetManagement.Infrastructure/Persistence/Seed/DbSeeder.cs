using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    private const string CoreRoleDefaultsInitializedKey = "rbac_core_role_defaults_initialized_v1";
    private const string DefaultPasswordBackfillKey = "security_default_password_backfill_v1";

    public static void Seed(AppDbContext db, string? configuredAdminPassword = null)
    {
        var originalTrackingBehavior = db.ChangeTracker.QueryTrackingBehavior;
        // 启动时的 DbContext 全局 NoTracking，种子需要更新已有行，必须显式开启跟踪。
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        try
        {
            EnsureOrganizationLevels(db);
            if (db.Users.Any())
            {
                EnsureDefaultPasswordUsersRequireChange(db);
                SeedIncremental(db);
                SeedTestMaterialModule(db);
                MarkCoreRoleDefaultsInitialized(db);
                return;
            }

            using var tx = db.Database.BeginTransaction();

            var permissions = RequiredPermissions()
                .Select(x => new Permission { Code = x.Code, Name = x.Name, Module = x.Module })
                .ToArray();

        db.Permissions.AddRange(permissions);

        var menus = new[]
        {
            new Menu { Id = 24, Name = "Home", Title = "首页", Path = "/home-root", Component = "BasicLayout", Icon = "lucide:house", Sort = 1 },
            new Menu { Id = 25, ParentId = 24, Name = "HomeWorkspace", Title = "首页", Path = "/home", Component = "/dashboard/workspace/index", Sort = 1 },
            new Menu { Id = 1, Name = "Asset", Title = "资产管理", Path = "/asset", Component = "BasicLayout", Icon = "lucide:package", Sort = 10 },
            new Menu { Id = 2, ParentId = 1, Name = "AssetList", Title = "资产列表", Path = "/asset/list", Component = "/asset/list/index", Sort = 11, PermissionCode = "asset:view" },
            // Id=3 资产层级菜单已删除,实际功能已整合到资产列表的层级视图中
            new Menu { Id = 18, ParentId = 1, Name = "AssetCategories", Title = "资产分类", Path = "/asset/categories", Component = "/asset/categories/index", Sort = 13, PermissionCode = "category:view" },
            new Menu { Id = 19, ParentId = 1, Name = "AssetLocations", Title = "存放位置", Path = "/asset/locations", Component = "/asset/locations/index", Sort = 14, PermissionCode = "location:view" },
            new Menu { Id = 4, Name = "Approval", Title = "审批管理", Path = "/approval", Component = "BasicLayout", Icon = "lucide:git-branch", Sort = 20 },
            new Menu { Id = 5, ParentId = 4, Name = "ApprovalPending", Title = "待我审批", Path = "/approval/pending", Component = "/approval/pending/index", Sort = 21, PermissionCode = "approval:handle" },
            new Menu { Id = 6, ParentId = 4, Name = "ApprovalMine", Title = "我的申请", Path = "/approval/mine", Component = "/approval/mine/index", Sort = 22, PermissionCode = "approval:view" },
            new Menu { Id = 23, ParentId = 4, Name = "ConfirmReturn", Title = "待确认归还", Path = "/approval/confirm-return", Component = "/approval/confirm-return/index", Sort = 23, PermissionCode = "approval:confirm-return" },
            new Menu { Id = 7, Name = "Report", Title = "报表统计", Path = "/report", Component = "BasicLayout", Icon = "lucide:chart-column", Sort = 30 },
            new Menu { Id = 8, ParentId = 7, Name = "ReportSummary", Title = "资产汇总", Path = "/report/summary", Component = "/report/summary/index", Sort = 31, PermissionCode = "report:view" },
            new Menu { Id = 9, ParentId = 7, Name = "ReportBorrow", Title = "借用明细", Path = "/report/borrow", Component = "/report/borrow/index", Sort = 32, PermissionCode = "report:view" },
            new Menu { Id = 22, ParentId = 7, Name = "ReportOverdue", Title = "逾期资产", Path = "/report/overdue", Component = "/report/overdue/index", Sort = 33, PermissionCode = "report:view" },
            new Menu { Id = 10, Name = "Admin", Title = "系统管理", Path = "/admin", Component = "BasicLayout", Icon = "lucide:settings", Sort = 40 },
            new Menu { Id = 11, ParentId = 10, Name = "AdminUsers", Title = "用户管理", Path = "/admin/users", Component = "/admin/users/index", Sort = 41, PermissionCode = "user:view" },
            new Menu { Id = 12, ParentId = 10, Name = "AdminRoles", Title = "角色管理", Path = "/admin/roles", Component = "/admin/roles/index", Sort = 42, PermissionCode = "role:view" },
            new Menu { Id = 20, ParentId = 10, Name = "AdminDepartments", Title = "组织架构", Path = "/admin/departments", Component = "/admin/departments/index", Sort = 43, PermissionCode = "department:view" },
            new Menu { Id = 13, ParentId = 10, Name = "AdminWorkflows", Title = "审批流程", Path = "/admin/workflows", Component = "/admin/workflows/index", Sort = 44, PermissionCode = "workflow:view" },
            new Menu { Id = 21, ParentId = 10, Name = "AdminSettings", Title = "系统参数", Path = "/admin/settings", Component = "/admin/settings/index", Sort = 45, PermissionCode = "setting:view" },
            new Menu { Id = 14, ParentId = 10, Name = "AdminAudit", Title = "审计日志", Path = "/admin/audit", Component = "/admin/audit/index", Sort = 46, PermissionCode = "audit:view" },
            new Menu { Id = 26, ParentId = 10, Name = "AdminBackups", Title = "数据库备份", Path = "/admin/backups", Component = "/admin/backups/index", Sort = 47, PermissionCode = "backup:manage" },
            new Menu { Id = 15, ParentId = 2, Name = "AssetCreateButton", Title = "新增资产按钮", Type = "button", Sort = 1, PermissionCode = "asset:create" },
            new Menu { Id = 16, ParentId = 2, Name = "AssetEditButton", Title = "编辑资产按钮", Type = "button", Sort = 2, PermissionCode = "asset:edit" },
            new Menu { Id = 17, ParentId = 2, Name = "AssetDeleteButton", Title = "删除资产按钮", Type = "button", Sort = 3, PermissionCode = "asset:delete" }
        };

        db.Menus.AddRange(menus);

        var roles = new[]
        {
            new Role { Code = "admin", Name = "系统管理员" },
            new Role { Code = "supervisor", Name = "部门主管" },
            new Role { Code = "employee", Name = "普通员工" }
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

        var allMenusForSeed = db.Menus.ToList();
        var homeMenu = allMenusForSeed.Single(x => x.Name == "Home");
        var homeWorkspaceMenu = allMenusForSeed.Single(x => x.Name == "HomeWorkspace");
        foreach (var pair in CoreRolePermissionMap())
        {
            var role = db.Roles.Single(x => x.Code == pair.Key);
            var perms = db.Permissions.Where(p => pair.Value.Contains(p.Code)).ToList();
            db.RolePermissions.AddRange(perms.Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id }));

            // 赋予权限码匹配的菜单 + 其所有祖先菜单（否则 vben 无父路由无法渲染子菜单）
            var menuIds = new HashSet<int> { homeMenu.Id, homeWorkspaceMenu.Id };
            foreach (var menu in allMenusForSeed.Where(m => m.PermissionCode != null
                         && pair.Value.Contains(m.PermissionCode)
                         && ShouldGrantMenu(pair.Key, m.Name)))
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

        // 初始管理员密码:优先取环境变量 ASSET_ADMIN_PASSWORD(生产部署应设置强密码),未设置时回退默认(仅供本地开发)
        var adminPassword = configuredAdminPassword;
        var usesDefaultAdminPassword = string.IsNullOrWhiteSpace(adminPassword)
            || adminPassword == AppConstants.DefaultUserPassword;
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = "123456";
        }
        var admin = new User
        {
            EmployeeNo = "1001",
            Name = "系统管理员",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            MustChangePassword = usesDefaultAdminPassword,
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
            new SystemSetting { Key = "audit_retention_months", Value = "12", Description = "审计日志保留月数（历史兼容）" },
            new SystemSetting { Key = "audit_retention_days", Value = "30", Description = "审计日志保留天数（7/14/30）" },
            new SystemSetting { Key = "audit_cleanup_enabled", Value = "true", Description = "是否启用审计日志定时清理" },
            new SystemSetting { Key = "audit_cleanup_time", Value = "02:10", Description = "审计日志定时清理时间" },
            new SystemSetting { Key = "database_backup_enabled", Value = "true", Description = "是否启用数据库定时备份" },
            new SystemSetting { Key = "database_backup_time", Value = "02:00", Description = "数据库定时备份时间" },
            new SystemSetting { Key = "database_backup_path", Value = "Backups", Description = "数据库备份目录" },
            new SystemSetting { Key = "database_backup_retention_days", Value = "30", Description = "数据库备份文件保留天数" },
            new SystemSetting { Key = "attachment_max_mb", Value = "5", Description = "附件大小限制 MB" },
            new SystemSetting { Key = AssetConditionDictionary.SettingKey, Value = AssetConditionDictionary.DefaultSerializedValue, Description = "资产目前状况数据字典" },
            new SystemSetting { Key = "page_size", Value = "20", Description = "默认每页记录数" },
            new SystemSetting { Key = "category_code_level1_length", Value = "2-6", Description = "资产分类一级编码段位数" },
            new SystemSetting { Key = "category_code_level1_regex", Value = "^[A-Za-z0-9]+$", Description = "资产分类一级编码段正则" },
            new SystemSetting { Key = "category_code_level2_length", Value = "2-6", Description = "资产分类二级编码段位数" },
            new SystemSetting { Key = "category_code_level2_regex", Value = "^[A-Za-z0-9]+$", Description = "资产分类二级编码段正则" },
            new SystemSetting { Key = "category_code_level3_length", Value = "2-6", Description = "资产分类三级编码段位数" },
            new SystemSetting { Key = "category_code_level3_regex", Value = "^[A-Za-z0-9]+$", Description = "资产分类三级编码段正则" },
            new SystemSetting { Key = DefaultPasswordBackfillKey, Value = "true", Description = "默认密码账号强制改密治理已完成" }
        );
        db.Workflows.AddRange(DefaultWorkflows());
        db.SaveChanges();
        tx.Commit();

            SeedTestMaterialModule(db);
            MarkCoreRoleDefaultsInitialized(db);
        }
        finally
        {
            db.ChangeTracker.QueryTrackingBehavior = originalTrackingBehavior;
        }
    }

    private static void SeedIncremental(AppDbContext db)
    {
        EnsureCoreRolePermissions(db);
        MigrateLegacyWorkflowRoleReferences(db);

        var defaultWorkflows = DefaultWorkflows();
        if (!db.Workflows.Any())
        {
            db.Workflows.AddRange(defaultWorkflows);
        }
        else
        {
            var defaultBorrowWorkflow = defaultWorkflows.Single(x => x.BizType == "borrow");
            var borrowWorkflow = db.Workflows.SingleOrDefault(x => x.BizType == "borrow" && x.IsActive);
            if (borrowWorkflow is not null && string.IsNullOrWhiteSpace(borrowWorkflow.BpmnXml))
            {
                borrowWorkflow.Name = defaultBorrowWorkflow.Name;
                borrowWorkflow.BpmnXml = defaultBorrowWorkflow.BpmnXml;
            }

            // 修复早期内置转让模板：语义层包含角色网关和 7 条顺序流，但 DI 层缺少
            // 网关/分支节点及 4 条连线，bpmn-js 会直接跳过这些无 DI 的元素。
            // 仅匹配该已知损坏特征，避免覆盖用户自行设计的转让流程。
            var transferWorkflow = db.Workflows.SingleOrDefault(x => x.BizType == "transfer" && x.IsActive);
            if (transferWorkflow?.BpmnXml is { } transferXml
                && transferXml.Contains("Gateway_applicantRole", StringComparison.Ordinal)
                && (!transferXml.Contains("bpmnElement=\"Gateway_applicantRole\"", StringComparison.Ordinal)
                    || transferXml.Contains("Flow_employeeDefault", StringComparison.Ordinal)))
            {
                var defaultTransferWorkflow = defaultWorkflows.Single(x => x.BizType == "transfer");
                var hasPendingInstances = db.ApprovalFlows.Any(x => x.WorkflowId == transferWorkflow.Id && x.Status == "pending")
                                          || db.MaterialFlows.Any(x => x.WorkflowId == transferWorkflow.Id && x.Status == "pending");
                if (hasPendingInstances)
                {
                    transferWorkflow.IsActive = false;
                    transferWorkflow.Name = $"{transferWorkflow.Name}（历史版本 {transferWorkflow.Id}）";
                    db.SaveChanges();
                    db.Workflows.Add(new WorkflowEntity
                    {
                        Name = defaultTransferWorkflow.Name,
                        BizType = defaultTransferWorkflow.BizType,
                        BpmnXml = defaultTransferWorkflow.BpmnXml,
                        IsActive = true
                    });
                }
                else
                {
                    transferWorkflow.Name = defaultTransferWorkflow.Name;
                    transferWorkflow.BpmnXml = defaultTransferWorkflow.BpmnXml;
                }
            }
        }

        if (!db.SystemSettings.Any(x => x.Key == "audit_retention_months"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "audit_retention_months", Value = "12", Description = "审计日志保留月数（历史兼容）" });
        }

        EnsureSetting(db, "audit_retention_days", "30", "审计日志保留天数（7/14/30）");
        EnsureSetting(db, "audit_cleanup_enabled", "true", "是否启用审计日志定时清理");
        EnsureSetting(db, "audit_cleanup_time", "02:10", "审计日志定时清理时间");
        EnsureSetting(db, "database_backup_enabled", "true", "是否启用数据库定时备份");
        EnsureSetting(db, "database_backup_time", "02:00", "数据库定时备份时间");
        EnsureSetting(db, "database_backup_path", "Backups", "数据库备份目录");
        EnsureSetting(db, "database_backup_retention_days", "30", "数据库备份文件保留天数");

        if (!db.SystemSettings.Any(x => x.Key == "attachment_max_mb"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "attachment_max_mb", Value = "5", Description = "附件大小限制 MB" });
        }

        if (!db.SystemSettings.Any(x => x.Key == "page_size"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "page_size", Value = "20", Description = "默认每页记录数" });
        }

        EnsureSetting(db, AssetConditionDictionary.SettingKey, AssetConditionDictionary.DefaultSerializedValue, "资产目前状况数据字典");

        EnsureSetting(db, "category_code_level1_length", "2-6", "资产分类一级编码段位数");
        EnsureSetting(db, "category_code_level1_regex", "^[A-Za-z0-9]+$", "资产分类一级编码段正则");
        EnsureSetting(db, "category_code_level2_length", "2-6", "资产分类二级编码段位数");
        EnsureSetting(db, "category_code_level2_regex", "^[A-Za-z0-9]+$", "资产分类二级编码段正则");
        EnsureSetting(db, "category_code_level3_length", "2-6", "资产分类三级编码段位数");
        EnsureSetting(db, "category_code_level3_regex", "^[A-Za-z0-9]+$", "资产分类三级编码段正则");

        var purgePermission = db.Permissions.SingleOrDefault(x => x.Code == "asset:purge");
        if (purgePermission is null)
        {
            purgePermission = new Permission { Code = "asset:purge", Name = "彻底删除资产/分类", Module = "asset" };
            db.Permissions.Add(purgePermission);
            db.SaveChanges();
        }
        else
        {
            purgePermission.Name = "彻底删除资产/分类";
            purgePermission.Module = "asset";
        }

        var admin = db.Roles.SingleOrDefault(x => x.Code == "admin");
        if (admin is not null
            && !db.RolePermissions.Any(x => x.RoleId == admin.Id && x.PermissionId == purgePermission.Id))
        {
            db.RolePermissions.Add(new RolePermission { RoleId = admin.Id, PermissionId = purgePermission.Id });
        }

        // 增量种子:恢复(撤销删除)权限,确保已有库补上并授予系统管理员 + 部门主管
        var restorePermission = db.Permissions.SingleOrDefault(x => x.Code == "asset:restore");
        if (restorePermission is null)
        {
            restorePermission = new Permission { Code = "asset:restore", Name = "恢复资产/分类", Module = "asset" };
            db.Permissions.Add(restorePermission);
            db.SaveChanges();
        }
        else
        {
            restorePermission.Name = "恢复资产/分类";
            restorePermission.Module = "asset";
        }

        foreach (var roleCode in new[] { "admin", "supervisor" })
        {
            var role = db.Roles.SingleOrDefault(x => x.Code == roleCode);
            if (role is not null
                && !db.RolePermissions.Any(x => x.RoleId == role.Id && x.PermissionId == restorePermission.Id))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = restorePermission.Id });
            }
        }

        var reportMenu = db.Menus.SingleOrDefault(x => x.Name == "Report");
        if (reportMenu is null)
        {
            reportMenu = new Menu
            {
                Name = "Report",
                Title = "报表统计",
                Path = "/report",
                Component = "BasicLayout",
                Icon = "lucide:chart-column",
                Sort = 30
            };
            db.Menus.Add(reportMenu);
            db.SaveChanges();
        }
        else
        {
            reportMenu.Title = "报表统计";
            reportMenu.Path = "/report";
            reportMenu.Component = "BasicLayout";
            reportMenu.Icon = "lucide:chart-column";
            reportMenu.Sort = 30;
            reportMenu.ParentId = null;
        }

        var reportAdminRole = db.Roles.SingleOrDefault(x => x.Code == "admin");
        if (reportAdminRole is not null
            && !db.RoleMenus.Any(x => x.RoleId == reportAdminRole.Id && x.MenuId == reportMenu.Id))
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = reportAdminRole.Id, MenuId = reportMenu.Id });
        }

        var reportOverdueMenu = db.Menus.SingleOrDefault(x => x.Name == "ReportOverdue");
        if (reportOverdueMenu is null)
        {
            reportOverdueMenu = new Menu
            {
                ParentId = reportMenu.Id,
                Name = "ReportOverdue",
                Title = "逾期资产",
                Path = "/report/overdue",
                Component = "/report/overdue/index",
                Sort = 33,
                PermissionCode = "report:view"
            };
            db.Menus.Add(reportOverdueMenu);
            db.SaveChanges();
        }
        else
        {
            reportOverdueMenu.ParentId = reportMenu.Id;
            reportOverdueMenu.Title = "逾期资产";
            reportOverdueMenu.Path = "/report/overdue";
            reportOverdueMenu.Component = "/report/overdue/index";
            reportOverdueMenu.Sort = 33;
            reportOverdueMenu.PermissionCode = "report:view";
        }

        if (reportAdminRole is not null
            && !db.RoleMenus.Any(x => x.RoleId == reportAdminRole.Id && x.MenuId == reportOverdueMenu.Id))
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = reportAdminRole.Id, MenuId = reportOverdueMenu.Id });
        }

        var adminMenu = EnsureRootMenu(db, "Admin", "系统管理", "/admin", "BasicLayout", "lucide:settings", 40);
        EnsureChildMenu(db, adminMenu, "AdminUsers", "用户管理", "/admin/users", "/admin/users/index", 41, "user:view");
        EnsureChildMenu(db, adminMenu, "AdminRoles", "角色管理", "/admin/roles", "/admin/roles/index", 42, "role:view");
        EnsureChildMenu(db, adminMenu, "AdminDepartments", "组织架构", "/admin/departments", "/admin/departments/index", 43, "department:view");
        EnsureChildMenu(db, adminMenu, "AdminWorkflows", "审批流程", "/admin/workflows", "/admin/workflows/index", 44, "workflow:view");
        EnsureChildMenu(db, adminMenu, "AdminSettings", "系统参数", "/admin/settings", "/admin/settings/index", 45, "setting:view");
        EnsureChildMenu(db, adminMenu, "AdminAudit", "审计日志", "/admin/audit", "/admin/audit/index", 46, "audit:view");
        EnsureChildMenu(db, adminMenu, "AdminBackups", "数据库备份", "/admin/backups", "/admin/backups/index", 47, "backup:manage");

        var existingHome = db.Menus.SingleOrDefault(x => x.Name == "Home");
        if (existingHome is null)
        {
            existingHome = new Menu
            {
                Name = "Home",
                Title = "首页",
                Path = "/home-root",
                Component = "BasicLayout",
                Icon = "lucide:house",
                Sort = 1
            };
            db.Menus.Add(existingHome);
            db.SaveChanges();
            foreach (var role in db.Roles.ToList())
            {
                if (!db.RoleMenus.Any(x => x.RoleId == role.Id && x.MenuId == existingHome.Id))
                {
                    db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = existingHome.Id });
                }
            }
        }
        else
        {
            existingHome.Title = "首页";
            existingHome.Path = "/home-root";
            existingHome.Component = "BasicLayout";
            existingHome.Icon = "lucide:house";
            existingHome.Sort = 1;
            existingHome.ParentId = null;
        }

        var existingHomeWorkspace = db.Menus.SingleOrDefault(x => x.Name == "HomeWorkspace");
        if (existingHomeWorkspace is null)
        {
            existingHomeWorkspace = new Menu
            {
                ParentId = existingHome.Id,
                Name = "HomeWorkspace",
                Title = "首页",
                Path = "/home",
                Component = "/dashboard/workspace/index",
                Sort = 1
            };
            db.Menus.Add(existingHomeWorkspace);
            db.SaveChanges();
            foreach (var role in db.Roles.ToList())
            {
                if (!db.RoleMenus.Any(x => x.RoleId == role.Id && x.MenuId == existingHomeWorkspace.Id))
                {
                    db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = existingHomeWorkspace.Id });
                }
            }
        }
        else
        {
            existingHomeWorkspace.ParentId = existingHome.Id;
            existingHomeWorkspace.Title = "首页";
            existingHomeWorkspace.Path = "/home";
            existingHomeWorkspace.Component = "/dashboard/workspace/index";
            existingHomeWorkspace.Sort = 1;
        }

        SyncMenuPermissionCodes(db);
        db.SaveChanges();
    }

    private static void EnsureDefaultPasswordUsersRequireChange(AppDbContext db)
    {
        if (db.SystemSettings.Any(x => x.Key == DefaultPasswordBackfillKey))
        {
            return;
        }

        foreach (var user in db.Users.AsTracking().Where(x => !x.MustChangePassword).ToList())
        {
            if (!BCrypt.Net.BCrypt.Verify(AppConstants.DefaultUserPassword, user.PasswordHash))
            {
                continue;
            }
            user.MustChangePassword = true;
            user.TokenVersion++;
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Key = DefaultPasswordBackfillKey,
            Value = "true",
            Description = "默认密码账号强制改密治理已完成"
        });
        db.SaveChanges();
    }

    private static void EnsureOrganizationLevels(AppDbContext db)
    {
        var defaults = new[]
        {
            new OrganizationLevel { Code = "company", Name = "公司/中心", Sort = 10, IsActive = true },
            new OrganizationLevel { Code = "division", Name = "事业部", Sort = 20, IsActive = true },
            new OrganizationLevel { Code = "department", Name = "部门", Sort = 30, IsActive = true },
            new OrganizationLevel { Code = "section", Name = "课别", Sort = 40, IsActive = true }
        };
        foreach (var item in defaults)
        {
            var existing = db.OrganizationLevels.SingleOrDefault(x => x.Code == item.Code);
            if (existing is null)
            {
                db.OrganizationLevels.Add(item);
            }
            else
            {
                existing.Name = item.Name;
                existing.Sort = item.Sort;
            }
        }
        db.SaveChanges();

        var levels = db.OrganizationLevels.ToDictionary(x => x.Code, x => x.Id);
        var departments = db.Departments.AsTracking().ToList();
        foreach (var department in departments.Where(x => !x.OrganizationLevelId.HasValue))
        {
            if (!department.ParentId.HasValue)
            {
                department.OrganizationLevelId = levels["company"];
                continue;
            }
            if (department.Name.Contains("事业部", StringComparison.Ordinal))
            {
                department.OrganizationLevelId = levels["division"];
                continue;
            }
            var parent = departments.SingleOrDefault(x => x.Id == department.ParentId.Value);
            var parentLevelCode = parent?.OrganizationLevelId is int parentLevelId
                ? levels.Single(x => x.Value == parentLevelId).Key
                : null;
            department.OrganizationLevelId = parentLevelCode is "company" or "division"
                ? levels["department"]
                : levels["section"];
        }
        db.SaveChanges();
    }

    private static void SyncMenuPermissionCodes(AppDbContext db)
    {
        var menuPermissions = new Dictionary<string, string>
        {
            ["AssetList"] = "asset:view",
            ["AssetCategories"] = "category:view",
            ["AssetLocations"] = "location:view",
            ["ApprovalPending"] = "approval:handle",
            ["ApprovalMine"] = "approval:view",
            ["ConfirmReturn"] = "approval:confirm-return",
            ["ReportSummary"] = "report:view",
            ["ReportBorrow"] = "report:view",
            ["ReportOverdue"] = "report:view",
            ["AdminUsers"] = "user:view",
            ["AdminRoles"] = "role:view",
            ["AdminWorkflows"] = "workflow:view",
            ["AdminAudit"] = "audit:view",
            ["AdminBackups"] = "backup:manage",
            ["AdminDepartments"] = "department:view",
            ["AdminSettings"] = "setting:view",
            ["AssetCreateButton"] = "asset:create",
            ["AssetEditButton"] = "asset:edit",
            ["AssetDeleteButton"] = "asset:delete",
            ["MaterialHome"] = "project:view",
            ["MaterialProjects"] = "project:view"
        };
        var menuTitles = new Dictionary<string, string>
        {
            ["ConfirmReturn"] = "待确认归还"
        };

        foreach (var menu in db.Menus.Where(x => menuPermissions.Keys.Contains(x.Name)).ToList())
        {
            menu.PermissionCode = menuPermissions[menu.Name];
            if (menuTitles.TryGetValue(menu.Name, out var title))
            {
                menu.Title = title;
            }
        }
    }

    private static Menu EnsureRootMenu(AppDbContext db, string name, string title, string path, string component, string icon, int sort)
    {
        var menu = db.Menus.SingleOrDefault(x => x.Name == name);
        if (menu is null)
        {
            menu = new Menu
            {
                Name = name,
                Title = title,
                Path = path,
                Component = component,
                Icon = icon,
                Sort = sort
            };
            db.Menus.Add(menu);
            db.SaveChanges();
            EnsureAdminMenu(db, menu);
            return menu;
        }

        menu.Title = title;
        menu.Path = path;
        menu.Component = component;
        menu.Icon = icon;
        menu.Sort = sort;
        menu.ParentId = null;
        EnsureAdminMenu(db, menu);
        return menu;
    }

    private static Menu EnsureChildMenu(
        AppDbContext db,
        Menu parent,
        string name,
        string title,
        string path,
        string component,
        int sort,
        string permissionCode)
    {
        var menu = db.Menus.SingleOrDefault(x => x.Name == name);
        if (menu is null)
        {
            menu = new Menu
            {
                ParentId = parent.Id,
                Name = name,
                Title = title,
                Path = path,
                Component = component,
                Sort = sort,
                PermissionCode = permissionCode
            };
            db.Menus.Add(menu);
            db.SaveChanges();
            EnsureAdminMenu(db, menu);
            return menu;
        }

        menu.ParentId = parent.Id;
        menu.Title = title;
        menu.Path = path;
        menu.Component = component;
        menu.Sort = sort;
        menu.PermissionCode = permissionCode;
        EnsureAdminMenu(db, menu);
        return menu;
    }

    private static void EnsureAdminMenu(AppDbContext db, Menu menu)
    {
        var admin = db.Roles.SingleOrDefault(x => x.Code == "admin");
        if (admin is not null
            && !db.RoleMenus.Any(x => x.RoleId == admin.Id && x.MenuId == menu.Id))
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = admin.Id, MenuId = menu.Id });
        }
    }

    private static (string Code, string Name, string Module)[] RequiredPermissions() => new[]
    {
        ("asset:view", "查看资产", "asset"),
        ("asset:create", "新增资产", "asset"),
        ("asset:edit", "编辑资产", "asset"),
        ("asset:delete", "删除资产", "asset"),
        ("asset:restore", "恢复资产", "asset"),
        ("asset:purge", "彻底删除资产", "asset"),
        ("asset:import", "导入资产", "asset"),
        ("asset:export", "导出资产", "asset"),

        ("category:view", "查看资产分类", "category"),
        ("category:create", "新增资产分类", "category"),
        ("category:edit", "编辑资产分类", "category"),
        ("category:delete", "删除资产分类", "category"),
        ("category:restore", "恢复资产分类", "category"),
        ("category:purge", "彻底删除资产分类", "category"),

        ("location:view", "查看存放位置", "location"),
        ("location:create", "新增存放位置", "location"),
        ("location:edit", "编辑存放位置", "location"),
        ("location:delete", "删除存放位置", "location"),

        ("file:upload", "上传文件", "file"),
        ("file:view", "查看文件", "file"),

        ("approval:create", "发起审批", "approval"),
        ("approval:view", "查看审批", "approval"),
        ("approval:handle", "处理审批", "approval"),
        ("approval:add-sign", "审批加签", "approval"),
        ("approval:transfer-sign", "审批转签", "approval"),
        ("approval:confirm-return", "确认归还接收", "approval"),

        ("report:view", "查看报表", "report"),
        ("report:export", "导出报表", "report"),
        ("report:remind", "逾期提醒", "report"),

        ("audit:view", "查看审计日志", "audit"),
        ("audit:export", "导出审计日志", "audit"),
        ("audit:cleanup", "清理审计日志", "audit"),
        ("backup:manage", "管理数据库备份", "backup"),
        ("setting:view", "查看系统参数", "setting"),
        ("setting:edit", "编辑系统参数", "setting"),

        ("user:view", "查看用户", "user"),
        ("user:create", "新增用户", "user"),
        ("user:edit", "编辑用户", "user"),
        ("user:assign-role", "分配用户角色", "user"),
        ("user:delete", "删除用户", "user"),
        ("user:reset-password", "重置用户密码", "user"),
        ("user:toggle-status", "启停用户", "user"),

        ("department:view", "查看部门", "department"),
        ("department:create", "新增部门", "department"),
        ("department:edit", "编辑部门", "department"),
        ("department:delete", "删除部门", "department"),

        ("role:view", "查看角色", "role"),
        ("role:create", "新增角色", "role"),
        ("role:edit", "编辑角色", "role"),
        ("role:delete", "删除角色", "role"),
        ("role:assign-permission", "分配角色权限", "role"),
        ("role:assign-menu", "分配角色菜单", "role"),
        ("permission:manage", "管理权限", "role"),
        ("menu:manage", "管理菜单", "role"),

        ("workflow:view", "查看工作流", "workflow"),
        ("workflow:create", "新增工作流", "workflow"),
        ("workflow:edit", "编辑工作流", "workflow"),
        ("workflow:delete", "删除工作流", "workflow"),
        ("workflow:design", "设计工作流", "workflow"),

        ("project:view", "查看测试项目", "project"),
        ("project:create", "新增测试项目", "project"),
        ("project:edit", "编辑测试项目", "project"),
        ("project:delete", "删除测试项目", "project"),
        ("project:restore", "恢复测试项目", "project"),
        ("project:purge", "彻底删除测试项目", "project"),
        ("project:option", "管理项目选项", "project"),
        ("project:followup", "管理项目跟进", "project"),
        ("project:manage", "管理测试项目", "project"),

        ("material:view", "查看测试料件", "material"),
        ("material:create", "新增测试料件", "material"),
        ("material:edit", "编辑测试料件", "material"),
        ("material:delete", "删除测试料件", "material"),
        ("material:restore", "恢复测试料件", "material"),
        ("material:purge", "彻底删除测试料件", "material"),
        ("material:return", "退回测试料件", "material"),
        ("material-flow:view", "查看料件流转", "material-flow"),
        ("material-flow:transfer", "发起料件流转", "material-flow"),
        ("material-flow:approve", "审批料件流转", "material-flow")
    };

    private static Dictionary<string, string[]> CoreRolePermissionMap() => new()
    {
        ["supervisor"] = new[]
        {
            "asset:view", "asset:create", "asset:edit", "asset:delete", "asset:restore", "asset:import", "asset:export",
            "category:view", "location:view", "file:upload", "file:view",
            "approval:create", "approval:view", "approval:handle", "approval:add-sign", "approval:transfer-sign", "approval:confirm-return",
            "report:view", "report:export",
            "department:view", "user:view",
            "project:view", "project:create", "project:edit", "project:delete", "project:restore", "project:followup", "project:manage",
            "material:view", "material:create", "material:edit", "material:delete", "material:restore", "material:return",
            "material-flow:view", "material-flow:transfer", "material-flow:approve"
        },
        ["employee"] = new[]
        {
            "asset:view", "category:view", "location:view", "file:view",
            "approval:create", "approval:view",
            "project:view", "material:view", "material-flow:view", "material-flow:transfer"
        }
    };

    private static void EnsureCoreRolePermissions(AppDbContext db)
    {
        EnsureCoreRoles(db);

        foreach (var (code, name, module) in RequiredPermissions())
        {
            var permission = db.Permissions.SingleOrDefault(x => x.Code == code);
            if (permission is null)
            {
                db.Permissions.Add(new Permission { Code = code, Name = name, Module = module });
            }
            else
            {
                permission.Name = name;
                permission.Module = module;
            }
        }
        db.SaveChanges();

        MigrateLegacyPermissions(db);

        EnsureAdminDefaultPermissionsAndMenus(db);

        if (!db.SystemSettings.Any(x => x.Key == CoreRoleDefaultsInitializedKey))
        {
            foreach (var (roleCode, permissionCodes) in CoreRolePermissionMap())
            {
                var role = db.Roles.SingleOrDefault(x => x.Code == roleCode);
                if (role is null) continue;

                EnsureRolePermissionsForMatrix(db, role, permissionCodes);

                EnsureRoleMenusForPermissions(db, role, permissionCodes);
            }
        }
        db.SaveChanges();
    }

    private static void MarkCoreRoleDefaultsInitialized(AppDbContext db)
    {
        if (db.SystemSettings.Any(x => x.Key == CoreRoleDefaultsInitializedKey)) return;
        db.SystemSettings.Add(new SystemSetting
        {
            Key = CoreRoleDefaultsInitializedKey,
            Value = "true",
            Description = "基础角色默认权限与菜单已初始化，后续保留管理员自定义授权"
        });
        db.SaveChanges();
    }

    private static void MigrateLegacyPermissions(AppDbContext db)
    {
        var legacyToCurrentCode = new Dictionary<string, string>
        {
            ["material:transfer"] = "material-flow:transfer",
            ["material:approve"] = "material-flow:approve",
            ["admin:user"] = "user:view",
            ["admin:role"] = "role:view",
            ["admin:audit"] = "audit:view",
            ["admin:setting"] = "setting:view"
        };
        var relatedCodes = legacyToCurrentCode.Keys
            .Concat(legacyToCurrentCode.Values)
            .ToHashSet(StringComparer.Ordinal);
        var permissions = db.Permissions
            .Where(x => relatedCodes.Contains(x.Code))
            .ToList();

        foreach (var (legacyCode, currentCode) in legacyToCurrentCode)
        {
            var legacyPermission = permissions.SingleOrDefault(x => x.Code == legacyCode);
            var currentPermission = permissions.SingleOrDefault(x => x.Code == currentCode);
            if (legacyPermission is null || currentPermission is null) continue;

            var legacyGrants = db.RolePermissions
                .Where(x => x.PermissionId == legacyPermission.Id)
                .ToList();
            foreach (var grant in legacyGrants)
            {
                if (!db.RolePermissions.Any(x => x.RoleId == grant.RoleId && x.PermissionId == currentPermission.Id))
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = grant.RoleId,
                        PermissionId = currentPermission.Id
                    });
                }
                db.RolePermissions.Remove(grant);
            }

            db.Permissions.Remove(legacyPermission);
        }

        db.SaveChanges();
    }

    private static void EnsureRolePermissionsForMatrix(AppDbContext db, Role role, string[] permissionCodes)
    {
        var desiredCodes = permissionCodes.ToHashSet(StringComparer.Ordinal);
        var desiredPermissions = db.Permissions.Where(x => desiredCodes.Contains(x.Code)).ToList();
        var existing = db.RolePermissions
            .Where(x => x.RoleId == role.Id)
            .ToList();

        foreach (var permission in desiredPermissions)
        {
            if (!existing.Any(x => x.PermissionId == permission.Id))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        var desiredPermissionIds = desiredPermissions.Select(x => x.Id).ToHashSet();
        foreach (var staleGrant in existing.Where(x => !desiredPermissionIds.Contains(x.PermissionId)))
        {
            db.RolePermissions.Remove(staleGrant);
        }
    }

    private static void EnsureAdminDefaultPermissionsAndMenus(AppDbContext db)
    {
        var admin = db.Roles.SingleOrDefault(x => x.Code == "admin");
        if (admin is null) return;

        var existingPermissionIds = db.RolePermissions
            .Where(x => x.RoleId == admin.Id)
            .Select(x => x.PermissionId)
            .ToHashSet();
        var missingPermissions = db.Permissions
            .Where(x => !existingPermissionIds.Contains(x.Id))
            .ToList();
        if (missingPermissions.Count > 0)
        {
            db.RolePermissions.AddRange(missingPermissions
                .Select(x => new RolePermission { RoleId = admin.Id, PermissionId = x.Id }));
        }

        var existingMenuIds = db.RoleMenus
            .Where(x => x.RoleId == admin.Id)
            .Select(x => x.MenuId)
            .ToHashSet();
        var missingMenus = db.Menus
            .Where(x => !existingMenuIds.Contains(x.Id))
            .ToList();
        if (missingMenus.Count > 0)
        {
            db.RoleMenus.AddRange(missingMenus
                .Select(x => new RoleMenu { RoleId = admin.Id, MenuId = x.Id }));
        }
    }

    private static void EnsureSetting(AppDbContext db, string key, string value, string description)
    {
        if (db.SystemSettings.Any(x => x.Key == key)) return;
        db.SystemSettings.Add(new SystemSetting
        {
            Key = key,
            Value = value,
            Description = description
        });
    }

    private static void EnsureCoreRoles(AppDbContext db)
    {
        var requiredRoles = new[]
        {
            ("admin", "系统管理员"),
            ("supervisor", "部门主管"),
            ("employee", "普通员工")
        };

        foreach (var (code, name) in requiredRoles)
        {
            var role = db.Roles.SingleOrDefault(x => x.Code == code);
            if (role is null)
            {
                db.Roles.Add(new Role { Code = code, Name = name, IsActive = true });
            }
            else
            {
                role.Name = name;
                role.IsActive = true;
            }
        }
        db.SaveChanges();
        MigrateLegacyRoles(db);
    }

    private static void MigrateLegacyRoles(AppDbContext db)
    {
        var supervisor = db.Roles.Single(x => x.Code == "supervisor");
        var admin = db.Roles.Single(x => x.Code == "admin");
        var legacyRoles = db.Roles.Where(x => x.Code == "warehouse" || x.Code == "dept_admin").ToList();
        var legacyRoleIds = legacyRoles.Select(x => x.Id).ToArray();
        var affectedUserIds = db.UserRoles
            .Where(x => legacyRoleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        foreach (var userId in affectedUserIds)
        {
            if (db.UserRoles.Any(x => x.UserId == userId && x.RoleId == admin.Id)) continue;
            db.UserRoles.RemoveRange(db.UserRoles.Where(x => x.UserId == userId && x.RoleId != supervisor.Id));
            if (!db.UserRoles.Any(x => x.UserId == userId && x.RoleId == supervisor.Id))
                db.UserRoles.Add(new UserRole { UserId = userId, RoleId = supervisor.Id });
        }
        foreach (var legacyRole in legacyRoles)
        {
            db.UserRoles.RemoveRange(db.UserRoles.Where(x => x.RoleId == legacyRole.Id));
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(x => x.RoleId == legacyRole.Id));
            db.RoleMenus.RemoveRange(db.RoleMenus.Where(x => x.RoleId == legacyRole.Id));
            db.Roles.Remove(legacyRole);
        }
        db.SaveChanges();
    }

    private static void MigrateLegacyWorkflowRoleReferences(AppDbContext db)
    {
        foreach (var workflow in db.Workflows.AsTracking().ToList())
        {
            if (string.IsNullOrEmpty(workflow.BpmnXml)) continue;
            workflow.BpmnXml = workflow.BpmnXml
                .Replace("warehouse", "supervisor", StringComparison.OrdinalIgnoreCase)
                .Replace("仓库管理员", "部门主管", StringComparison.Ordinal)
                .Replace("资产管理员", "部门主管", StringComparison.Ordinal)
                .Replace(
                    "camunda:candidateGroups=\"supervisor\"",
                    "camunda:candidateGroups=\"role:supervisor\"",
                    StringComparison.Ordinal);
        }
        db.SaveChanges();
    }

    private static void EnsureRoleMenusForPermissions(AppDbContext db, Role role, string[] permissionCodes)
    {
        var allMenus = db.Menus.ToList();
        var menuIds = new HashSet<int>();
        foreach (var menu in allMenus.Where(x => x.PermissionCode != null
                     && permissionCodes.Contains(x.PermissionCode)
                     && ShouldGrantMenu(role.Code, x.Name)))
        {
            menuIds.Add(menu.Id);
            var cursor = menu;
            while (cursor.ParentId.HasValue)
            {
                menuIds.Add(cursor.ParentId.Value);
                cursor = allMenus.First(x => x.Id == cursor.ParentId.Value);
            }
        }

        var home = allMenus.SingleOrDefault(x => x.Name == "Home");
        if (home is not null) menuIds.Add(home.Id);
        var homeWorkspace = allMenus.SingleOrDefault(x => x.Name == "HomeWorkspace");
        if (homeWorkspace is not null) menuIds.Add(homeWorkspace.Id);

        var staleMenus = db.RoleMenus.Where(x => x.RoleId == role.Id && !menuIds.Contains(x.MenuId)).ToList();
        db.RoleMenus.RemoveRange(staleMenus);

        foreach (var menuId in menuIds)
        {
            if (!db.RoleMenus.Any(x => x.RoleId == role.Id && x.MenuId == menuId))
            {
                db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = menuId });
            }
        }
    }

    private static bool ShouldGrantMenu(string roleCode, string menuName)
        => roleCode != "employee" || menuName is not "AssetCategories" and not "AssetLocations";

    public static void SeedTestMaterialModule(AppDbContext db)
    {
        // ---- 1. 权限码 ----
        var materialPermissions = RequiredPermissions()
            .Where(x => x.Module is "material" or "material-flow" or "project")
            .ToArray();
        var permByCode = new Dictionary<string, Permission>();
        foreach (var (code, name, module) in materialPermissions)
        {
            var perm = db.Permissions.SingleOrDefault(x => x.Code == code);
            if (perm is null)
            {
                perm = new Permission { Code = code, Name = name, Module = module };
                db.Permissions.Add(perm);
            }
            else
            {
                perm.Name = name;
                perm.Module = module;
            }
            permByCode[code] = perm;
        }
        db.SaveChanges();

        // ---- 2. 角色-权限映射 ----
        var roleGrants = new Dictionary<string, string[]>
        {
            ["admin"] = materialPermissions.Select(p => p.Code).ToArray(),
        };
        if (!db.SystemSettings.Any(x => x.Key == CoreRoleDefaultsInitializedKey))
        {
            roleGrants["supervisor"] = CoreRolePermissionMap()["supervisor"]
                .Where(x => x.StartsWith("material") || x.StartsWith("project"))
                .ToArray();
            roleGrants["employee"] = CoreRolePermissionMap()["employee"]
                .Where(x => x.StartsWith("material") || x.StartsWith("project"))
                .ToArray();
        }
        foreach (var (roleCode, codes) in roleGrants)
        {
            var role = db.Roles.SingleOrDefault(x => x.Code == roleCode);
            if (role is null) continue;
            foreach (var code in codes)
            {
                var perm = permByCode[code];
                if (!db.RolePermissions.Any(x => x.RoleId == role.Id && x.PermissionId == perm.Id))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }
        db.SaveChanges();

        // ---- 3. 菜单(一级入口"新产品新技术"+ 单一项目入口)----
        var rootMenu = db.Menus.SingleOrDefault(x => x.Name == "Material");
        if (rootMenu is null)
        {
            rootMenu = new Menu
            {
                Name = "Material",
                Title = "新产品新技术",
                Path = "/material",
                Component = "BasicLayout",
                Icon = "lucide:flask-conical",
                Sort = 15
            };
            db.Menus.Add(rootMenu);
            db.SaveChanges();
        }
        else
        {
            rootMenu.Title = "新产品新技术";
            rootMenu.Path = "/material";
            rootMenu.Component = "BasicLayout";
            rootMenu.Icon = "lucide:flask-conical";
            rootMenu.Sort = 15;
            rootMenu.ParentId = null;
        }
        db.SaveChanges();

        void EnsureChild(string name, string title, string path, string component, int sort, string permCode)
        {
            var existing = db.Menus.SingleOrDefault(x => x.Name == name);
            if (existing is not null)
            {
                existing.ParentId = rootMenu.Id;
                existing.Title = title;
                existing.Path = path;
                existing.Component = component;
                existing.Sort = sort;
                existing.PermissionCode = permCode;
                return;
            }
            var menu = new Menu
            {
                ParentId = rootMenu.Id,
                Name = name, Title = title, Path = path, Component = component,
                Sort = sort, PermissionCode = permCode
            };
            db.Menus.Add(menu);
            db.SaveChanges();
        }

        EnsureChild("MaterialHome", "项目总览", "/material/home", "/material/home/index", 16, "project:view");
        EnsureChild("MaterialProjects", "测试项目", "/material/projects", "/material/projects/index", 17, "project:view");
        db.SaveChanges();

        // 按权限矩阵逐项补齐根菜单 + 子菜单；已有任意菜单不能阻止新模块增量授权。
        foreach (var roleCode in roleGrants.Keys)
        {
            var role = db.Roles.SingleOrDefault(x => x.Code == roleCode);
            if (role is null) continue;
            var grantedCodes = roleGrants.GetValueOrDefault(roleCode, Array.Empty<string>()).ToHashSet();
            var childMenus = db.Menus.Where(x => x.ParentId == rootMenu.Id && x.PermissionCode != null && grantedCodes.Contains(x.PermissionCode!)).ToList();
            if (childMenus.Count == 0) continue;
            if (!db.RoleMenus.Any(x => x.RoleId == role.Id && x.MenuId == rootMenu.Id))
                db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = rootMenu.Id });
            foreach (var menu in childMenus)
                if (!db.RoleMenus.Any(x => x.RoleId == role.Id && x.MenuId == menu.Id))
                    db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = menu.Id });
        }
        db.SaveChanges();

        // ---- 4. 系统参数:流转审批全局开关(默认关闭)----
        if (!db.SystemSettings.Any(x => x.Key == "material.transfer.approval.enabled"))
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = "material.transfer.approval.enabled",
                Value = "false",
                Description = "是否启用测试料件转移审批(false=直接转移)"
            });
        }

        // ---- 4.1 测试项目配置项 ----
        EnsureProjectOption(db, "project_type", "prototype", "样机测试", 1);
        EnsureProjectOption(db, "project_type", "trial", "试产验证", 2);
        EnsureProjectOption(db, "project_type", "issue", "问题验证", 3);
        EnsureProjectOption(db, "project_progress", "planning", "计划中", 1);
        EnsureProjectOption(db, "project_progress", "testing", "测试中", 2);
        EnsureProjectOption(db, "project_progress", "landing", "落地跟进", 3);
        EnsureProjectOption(db, "project_progress", "closed", "已结案", 4);

        // ---- 5. 默认 BPMN 工作流模板(material_transfer)----
        var defaultApproverId = db.Users
            .Where(x => x.IsActive && x.UserRoles.Any(ur => ur.Role.IsActive && ur.Role.Code == "admin"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefault();
        if (defaultApproverId <= 0)
            throw new InvalidOperationException("测试料件默认审批流程缺少启用状态的系统管理员");
        var materialTransferBpmnXml = MaterialTransferBpmnXmlTemplate.Replace(
            "__DEFAULT_APPROVER__",
            $"user:{defaultApproverId}",
            StringComparison.Ordinal);
        var materialWorkflow = db.Workflows.SingleOrDefault(x => x.BizType == "material_transfer");
        if (materialWorkflow is null)
        {
            db.Workflows.Add(new WorkflowEntity
            {
                Name = "测试料件流转流程",
                BizType = "material_transfer",
                BpmnXml = materialTransferBpmnXml
            });
        }
        else if (IsLegacyMaterialTransferWorkflow(materialWorkflow.BpmnXml))
        {
            materialWorkflow.Name = "测试料件流转流程";
            materialWorkflow.BpmnXml = materialTransferBpmnXml;
        }
        else if (materialWorkflow.BpmnXml?.Contains("id=\"Task_projectOwnerSpecified\"", StringComparison.Ordinal) == true)
        {
            materialWorkflow.BpmnXml = materialWorkflow.BpmnXml.Replace(
                "camunda:assignee=\"1001\"",
                $"camunda:assignee=\"user:{defaultApproverId}\"",
                StringComparison.Ordinal);
        }
        db.SaveChanges();
    }

    private static bool IsLegacyMaterialTransferWorkflow(string? bpmnXml)
        => !string.IsNullOrWhiteSpace(bpmnXml)
           && bpmnXml.Contains("Task_deptManager", StringComparison.Ordinal)
           && !bpmnXml.Contains("Gateway_projectOwner", StringComparison.Ordinal)
           && !bpmnXml.Contains("Task_projectOwnerSpecified", StringComparison.Ordinal);

    private static void EnsureProjectOption(AppDbContext db, string kind, string code, string label, int sort)
    {
        var option = db.TestProjectOptions.SingleOrDefault(x => x.Kind == kind && x.Code == code);
        if (option is null)
        {
            db.TestProjectOptions.Add(new TestProjectOption
            {
                Kind = kind,
                Code = code,
                Label = label,
                Sort = sort,
                IsActive = true
            });
            return;
        }

        option.Label = label;
        option.Sort = sort;
    }

    private const string MaterialTransferBpmnXmlTemplate = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
                  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
                  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn""
                  id=""Definitions_material_transfer"">
  <bpmn:process id=""Process_material_transfer"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" name=""发起料件流转"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:exclusiveGateway id=""Gateway_projectOwner"" name=""是否项目负责人"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_projectOwner</bpmn:outgoing>
      <bpmn:outgoing>Flow_nonOwner</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_projectOwnerSpecified"" name=""指定人员审批"" camunda:assignee=""__DEFAULT_APPROVER__"">
      <bpmn:incoming>Flow_projectOwner</bpmn:incoming>
      <bpmn:outgoing>Flow_specified_to_end</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_deptManager"" name=""部门负责人审批"" camunda:assignee=""deptManager"">
      <bpmn:incoming>Flow_nonOwner</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
      <bpmn:incoming>Flow_specified_to_end</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Gateway_projectOwner"" />
    <bpmn:sequenceFlow id=""Flow_projectOwner"" sourceRef=""Gateway_projectOwner"" targetRef=""Task_projectOwnerSpecified"">
      <bpmn:conditionExpression>${isProjectOwner} == ""true""</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_nonOwner"" sourceRef=""Gateway_projectOwner"" targetRef=""Task_deptManager"" />
    <bpmn:sequenceFlow id=""Flow_specified_to_end"" sourceRef=""Task_projectOwnerSpecified"" targetRef=""EndEvent_1"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_deptManager"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_material_transfer"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""152"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_projectOwner_di"" bpmnElement=""Gateway_projectOwner"">
        <dc:Bounds x=""230"" y=""95"" width=""50"" height=""50"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_projectOwnerSpecified_di"" bpmnElement=""Task_projectOwnerSpecified"">
        <dc:Bounds x=""340"" y=""20"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_deptManager_di"" bpmnElement=""Task_deptManager"">
        <dc:Bounds x=""340"" y=""140"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""EndEvent_1_di"" bpmnElement=""EndEvent_1"">
        <dc:Bounds x=""520"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""188"" y=""120"" />
        <di:waypoint x=""230"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_projectOwner_di"" bpmnElement=""Flow_projectOwner"">
        <di:waypoint x=""255"" y=""95"" />
        <di:waypoint x=""255"" y=""60"" />
        <di:waypoint x=""340"" y=""60"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_nonOwner_di"" bpmnElement=""Flow_nonOwner"">
        <di:waypoint x=""255"" y=""145"" />
        <di:waypoint x=""255"" y=""180"" />
        <di:waypoint x=""340"" y=""180"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_specified_to_end_di"" bpmnElement=""Flow_specified_to_end"">
        <di:waypoint x=""440"" y=""60"" />
        <di:waypoint x=""500"" y=""60"" />
        <di:waypoint x=""500"" y=""120"" />
        <di:waypoint x=""520"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2"">
        <di:waypoint x=""440"" y=""180"" />
        <di:waypoint x=""500"" y=""180"" />
        <di:waypoint x=""500"" y=""120"" />
        <di:waypoint x=""520"" y=""120"" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    private static WorkflowEntity[] DefaultWorkflows() => new[]
    {
        new WorkflowEntity
        {
            Name = "资产借用流程",
            BizType = "borrow",
            BpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
                  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
                  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn""
                  id=""Definitions_borrow"">
  <bpmn:process id=""Process_borrow"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" name=""发起借用申请"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id=""Task_supervisor"" name=""直属主管审批"" camunda:assignee=""supervisor"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_supervisor"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_supervisor"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_borrow"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""152"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_supervisor_di"" bpmnElement=""Task_supervisor"">
        <dc:Bounds x=""240"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""EndEvent_1_di"" bpmnElement=""EndEvent_1"">
        <dc:Bounds x=""392"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""188"" y=""120"" />
        <di:waypoint x=""240"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2"">
        <di:waypoint x=""340"" y=""120"" />
        <di:waypoint x=""392"" y=""120"" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>"
        },
        new WorkflowEntity
        {
            Name = "资产转让流程",
            BizType = "transfer",
            BpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
                  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
                  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn""
                  id=""Definitions_transfer"">
  <bpmn:process id=""Process_transfer"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" name=""发起转让申请"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:exclusiveGateway id=""Gateway_applicantRole"" name=""申请人角色判断"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_admin</bpmn:outgoing>
      <bpmn:outgoing>Flow_supervisorRole</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_adminRole"" name=""部门主管审批"" camunda:candidateGroups=""role:supervisor"">
      <bpmn:incoming>Flow_admin</bpmn:incoming>
      <bpmn:outgoing>Flow_admin_to_receiver</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_supervisorRole"" name=""部门负责人审批"" camunda:assignee=""deptManager"">
      <bpmn:incoming>Flow_supervisorRole</bpmn:incoming>
      <bpmn:outgoing>Flow_supervisor_to_receiver</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_receiver"" name=""接收部门负责人审批"" camunda:assignee=""deptManager"">
      <bpmn:incoming>Flow_admin_to_receiver</bpmn:incoming>
      <bpmn:incoming>Flow_supervisor_to_receiver</bpmn:incoming>
      <bpmn:outgoing>Flow_3</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_3</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Gateway_applicantRole"" />
    <bpmn:sequenceFlow id=""Flow_admin"" sourceRef=""Gateway_applicantRole"" targetRef=""Task_adminRole"">
      <bpmn:conditionExpression>${applicantRole} == ""admin""</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_supervisorRole"" sourceRef=""Gateway_applicantRole"" targetRef=""Task_supervisorRole"" />
    <bpmn:sequenceFlow id=""Flow_admin_to_receiver"" sourceRef=""Task_adminRole"" targetRef=""Task_receiver"" />
    <bpmn:sequenceFlow id=""Flow_supervisor_to_receiver"" sourceRef=""Task_supervisorRole"" targetRef=""Task_receiver"" />
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_receiver"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_transfer"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""100"" y=""222"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_applicantRole_di"" bpmnElement=""Gateway_applicantRole"" isMarkerVisible=""true"">
        <dc:Bounds x=""210"" y=""215"" width=""50"" height=""50"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_adminRole_di"" bpmnElement=""Task_adminRole"">
        <dc:Bounds x=""340"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_supervisorRole_di"" bpmnElement=""Task_supervisorRole"">
        <dc:Bounds x=""340"" y=""200"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_receiver_di"" bpmnElement=""Task_receiver"">
        <dc:Bounds x=""540"" y=""200"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""EndEvent_1_di"" bpmnElement=""EndEvent_1"">
        <dc:Bounds x=""720"" y=""222"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""136"" y=""240"" />
        <di:waypoint x=""210"" y=""240"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_admin_di"" bpmnElement=""Flow_admin"">
        <di:waypoint x=""235"" y=""215"" />
        <di:waypoint x=""235"" y=""120"" />
        <di:waypoint x=""340"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_supervisorRole_di"" bpmnElement=""Flow_supervisorRole"">
        <di:waypoint x=""260"" y=""240"" />
        <di:waypoint x=""340"" y=""240"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_admin_to_receiver_di"" bpmnElement=""Flow_admin_to_receiver"">
        <di:waypoint x=""440"" y=""120"" />
        <di:waypoint x=""490"" y=""120"" />
        <di:waypoint x=""490"" y=""220"" />
        <di:waypoint x=""540"" y=""220"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_supervisor_to_receiver_di"" bpmnElement=""Flow_supervisor_to_receiver"">
        <di:waypoint x=""440"" y=""240"" />
        <di:waypoint x=""540"" y=""240"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3"">
        <di:waypoint x=""640"" y=""240"" />
        <di:waypoint x=""720"" y=""240"" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>"
        },
        new WorkflowEntity
        {
            Name = "资产归还流程",
            BizType = "return",
            BpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
                  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
                  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn""
                  id=""Definitions_return"">
  <bpmn:process id=""Process_return"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" name=""发起归还申请"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id=""Task_supervisor"" name=""部门主管确认"" camunda:candidateGroups=""role:supervisor"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_supervisor"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_supervisor"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_return"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""152"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_supervisor_di"" bpmnElement=""Task_supervisor"">
        <dc:Bounds x=""240"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""EndEvent_1_di"" bpmnElement=""EndEvent_1"">
        <dc:Bounds x=""392"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""188"" y=""120"" />
        <di:waypoint x=""240"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2"">
        <di:waypoint x=""340"" y=""120"" />
        <di:waypoint x=""392"" y=""120"" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>"
        }
    };
}
