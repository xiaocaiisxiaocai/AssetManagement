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
            new Menu { Id = 24, Name = "Home", Title = "首页", Path = "/home-root", Component = "BasicLayout", Icon = "lucide:house", Sort = 1 },
            new Menu { Id = 25, ParentId = 24, Name = "HomeWorkspace", Title = "首页", Path = "/home", Component = "/dashboard/workspace/index", Sort = 1 },
            new Menu { Id = 1, Name = "Asset", Title = "资产管理", Path = "/asset", Component = "BasicLayout", Icon = "lucide:package", Sort = 10 },
            new Menu { Id = 2, ParentId = 1, Name = "AssetList", Title = "资产列表", Path = "/asset/list", Component = "/asset/list/index", Sort = 11, PermissionCode = "asset:view" },
            // Id=3 资产层级菜单已删除,实际功能已整合到资产列表的层级视图中
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
        var homeMenu = allMenusForSeed.Single(x => x.Name == "Home");
        var homeWorkspaceMenu = allMenusForSeed.Single(x => x.Name == "HomeWorkspace");
        foreach (var pair in rolePermissionMap)
        {
            var role = db.Roles.Single(x => x.Code == pair.Key);
            var perms = db.Permissions.Where(p => pair.Value.Contains(p.Code)).ToList();
            db.RolePermissions.AddRange(perms.Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id }));

            // 赋予权限码匹配的菜单 + 其所有祖先菜单（否则 vben 无父路由无法渲染子菜单）
            var menuIds = new HashSet<int> { homeMenu.Id, homeWorkspaceMenu.Id };
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

        // 初始管理员密码:优先取环境变量 ASSET_ADMIN_PASSWORD(生产部署应设置强密码),未设置时回退默认(仅供本地开发)
        var adminPassword = Environment.GetEnvironmentVariable("ASSET_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = "123456";
        }
        var admin = new User
        {
            EmployeeNo = "1001",
            Name = "系统管理员",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
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
            new SystemSetting { Key = "attachment_max_mb", Value = "5", Description = "附件大小限制 MB" },
            new SystemSetting { Key = "page_size", Value = "20", Description = "默认每页记录数" }
        );
        db.Workflows.AddRange(DefaultWorkflows());
        db.SaveChanges();
    }

    private static void SeedIncremental(AppDbContext db)
    {
        var defaultWorkflows = DefaultWorkflows();
        if (!db.Workflows.Any())
        {
            db.Workflows.AddRange(defaultWorkflows);
        }
        else
        {
            var defaultBorrowWorkflow = defaultWorkflows.Single(x => x.BizType == "borrow");
            var borrowWorkflow = db.Workflows.SingleOrDefault(x => x.BizType == "borrow");
            if (borrowWorkflow is not null)
            {
                borrowWorkflow.Name = defaultBorrowWorkflow.Name;
                borrowWorkflow.BpmnXml = defaultBorrowWorkflow.BpmnXml;
            }
        }

        if (!db.SystemSettings.Any(x => x.Key == "audit_retention_months"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "audit_retention_months", Value = "12", Description = "审计日志保留月数" });
        }

        if (!db.SystemSettings.Any(x => x.Key == "attachment_max_mb"))
        {
            db.SystemSettings.Add(new SystemSetting { Key = "attachment_max_mb", Value = "5", Description = "附件大小限制 MB" });
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

        var nextMenuId = db.Menus.Any() ? db.Menus.Max(x => x.Id) + 1 : 1;
        var existingHome = db.Menus.SingleOrDefault(x => x.Name == "Home");
        if (existingHome is null)
        {
            existingHome = new Menu
            {
                Id = nextMenuId++,
                Name = "Home",
                Title = "首页",
                Path = "/home-root",
                Component = "BasicLayout",
                Icon = "lucide:house",
                Sort = 1
            };
            db.Menus.Add(existingHome);
            foreach (var role in db.Roles.ToList())
            {
                db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = existingHome.Id });
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
                Id = nextMenuId++,
                ParentId = existingHome.Id,
                Name = "HomeWorkspace",
                Title = "首页",
                Path = "/home",
                Component = "/dashboard/workspace/index",
                Sort = 1
            };
            db.Menus.Add(existingHomeWorkspace);
            foreach (var role in db.Roles.ToList())
            {
                db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = existingHomeWorkspace.Id });
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

        db.SaveChanges();
    }

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
    <bpmn:userTask id=""Task_supervisor"" name=""直属主管审批"" camunda:assignee=""supervisor"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_receiver"" name=""接收部门负责人审批"" camunda:assignee=""deptManager"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
      <bpmn:outgoing>Flow_3</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_3</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_supervisor"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_supervisor"" targetRef=""Task_receiver"" />
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_receiver"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_transfer"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""152"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_supervisor_di"" bpmnElement=""Task_supervisor"">
        <dc:Bounds x=""240"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_receiver_di"" bpmnElement=""Task_receiver"">
        <dc:Bounds x=""400"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""EndEvent_1_di"" bpmnElement=""EndEvent_1"">
        <dc:Bounds x=""552"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""188"" y=""120"" />
        <di:waypoint x=""240"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2"">
        <di:waypoint x=""340"" y=""120"" />
        <di:waypoint x=""400"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3"">
        <di:waypoint x=""500"" y=""120"" />
        <di:waypoint x=""552"" y=""120"" />
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
    <bpmn:userTask id=""Task_warehouse"" name=""资产管理员确认"" camunda:candidateGroups=""warehouse"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_warehouse"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_warehouse"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_return"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""152"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_warehouse_di"" bpmnElement=""Task_warehouse"">
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
