using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Xml.Linq;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence.Seed;
using FluentAssertions;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Tests.Workflow;

public class BpmnEngineRegressionTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public BpmnEngineRegressionTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public void Default_workflow_bpmn_can_be_parsed()
    {
        var method = typeof(DbSeeder).GetMethod("DefaultWorkflows", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("测试需要读取默认工作流种子数据");
        var workflows = ((IEnumerable<WorkflowEntity>)method!.Invoke(null, null)!).ToList();

        foreach (var workflow in workflows)
        {
            var act = () => BpmnParser.Parse(workflow.BpmnXml!);

            act.Should().NotThrow($"默认流程 {workflow.BizType} 必须是合法 BPMN XML");
        }
    }

    [Fact]
    public void Applicant_department_condition_only_matches_same_department()
    {
        var process = BpmnParser.Parse(DepartmentGatewayBpmn("技术部", "Task_Tech", "Task_Other"));
        var flow = new TestFlow { ApplicantDept = "财务部" };

        BpmnEngine.Start(flow, process);

        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_Other");
        flow.BpmnTokens.Should().NotContainKey("Task_Tech");
    }

    [Fact]
    public void Default_workflow_bpmn_has_complete_di_for_nodes_and_sequence_flows()
    {
        var method = typeof(DbSeeder).GetMethod("DefaultWorkflows", BindingFlags.NonPublic | BindingFlags.Static)!;
        var workflows = ((IEnumerable<WorkflowEntity>)method.Invoke(null, null)!).ToList();
        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        XNamespace bpmndi = "http://www.omg.org/spec/BPMN/20100524/DI";

        foreach (var workflow in workflows)
        {
            var document = XDocument.Parse(workflow.BpmnXml!);
            var nodeNames = new[]
            {
                "startEvent", "endEvent", "userTask", "serviceTask",
                "exclusiveGateway", "inclusiveGateway", "parallelGateway"
            };
            var semanticNodeIds = document.Descendants()
                .Where(x => nodeNames.Contains(x.Name.LocalName))
                .Select(x => (string?)x.Attribute("id"))
                .Where(x => x is not null)
                .ToHashSet();
            var shapeIds = document.Descendants(bpmndi + "BPMNShape")
                .Select(x => (string?)x.Attribute("bpmnElement"))
                .Where(x => x is not null)
                .ToHashSet();
            var flowIds = document.Descendants(bpmn + "sequenceFlow")
                .Select(x => (string?)x.Attribute("id"))
                .Where(x => x is not null)
                .ToHashSet();
            var edgeIds = document.Descendants(bpmndi + "BPMNEdge")
                .Select(x => (string?)x.Attribute("bpmnElement"))
                .Where(x => x is not null)
                .ToHashSet();

            semanticNodeIds.Should().BeSubsetOf(shapeIds,
                $"默认流程 {workflow.BizType} 的每个节点都必须有 BPMNShape");
            flowIds.Should().BeSubsetOf(edgeIds,
                $"默认流程 {workflow.BizType} 的每条顺序流都必须有 BPMNEdge");
            edgeIds.Should().BeSubsetOf(flowIds,
                $"默认流程 {workflow.BizType} 不应包含指向不存在顺序流的 BPMNEdge");
        }
    }

    [Fact]
    public void Applicant_role_condition_can_route_workflow_branch()
    {
        var process = BpmnParser.Parse(ApplicantRoleGatewayBpmn("supervisor", "Task_SupervisorPath", "Task_Default"));
        var flow = new TestFlow
        {
            Context = new Dictionary<string, string>
            {
                ["applicantRole"] = "supervisor"
            }
        };

        BpmnEngine.Start(flow, process);

        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_SupervisorPath");
        flow.BpmnTokens.Should().NotContainKey("Task_Default");
    }

    [Theory]
    [InlineData("true", "true", "Task_SectionManager")]
    [InlineData("false", "true", "Task_DepartmentManager")]
    [InlineData("false", "false", null)]
    public void Organization_approval_conditions_route_without_department_names(
        string requiresSectionApproval,
        string requiresDepartmentApproval,
        string? expectedNode)
    {
        var process = BpmnParser.Parse(OrganizationApprovalBpmn);
        var flow = new TestFlow
        {
            Context = new Dictionary<string, string>
            {
                ["requiresSectionApproval"] = requiresSectionApproval,
                ["requiresDepartmentApproval"] = requiresDepartmentApproval
            }
        };

        BpmnEngine.Start(flow, process);

        if (expectedNode is null)
        {
            flow.Status.Should().Be("approved");
            flow.CurrentNodeIds.Should().BeEmpty();
        }
        else
        {
            flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be(expectedNode);
        }
    }

    [Fact]
    public async Task Applicant_role_condition_routes_supervisor_to_dept_manager()
    {
        await Login();
        var deptAdminRole = await Role("supervisor");
        var supervisorRole = await Role("supervisor");
        var dept = await CreateDepartment("主管转借部门");
        var deptAdmin = await CreateUser("部门管理员", deptAdminRole.Id, dept.Data!.Id);
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{dept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = dept.Data.Name,
            ManagerId = deptAdmin.Data!.Id,
            IsActive = true
        });
        var applicant = await CreateUser("主管申请人", supervisorRole.Id, dept.Data.Id);
        var workflow = await CreateWorkflow("role_branch", ApplicantRoleWorkflowBpmn("supervisor", "deptManager", "supervisor"));
        var asset = await CreateAsset(dept.Data.Id);

        Auth(await LoginToken(applicant.Data!.EmployeeNo, "123456"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.Data!.BizType,
            AssetId = asset.Data!.Id,
            Reason = "测试申请人角色分支"
        });

        flow.Data!.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_SupervisorRole");

        Auth(await LoginToken(deptAdmin.Data!.EmployeeNo, "123456"));
        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "同意" });

        approved.Code.Should().Be(0, approved.Message);
        approved.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Applicant_role_condition_routes_admin_to_configured_role()
    {
        await Login();
        var warehouseRole = await Role("supervisor");
        var adminRole = await Role("admin");
        var dept = await CreateDepartment("管理员转借部门");
        var warehouse = await CreateUser("部门主管审批人", warehouseRole.Id, dept.Data!.Id);
        var applicant = await CreateUser("管理员申请人", adminRole.Id);
        var workflow = await CreateWorkflow("admin_branch", ApplicantRoleToRoleWorkflowBpmn("admin", "supervisor", "supervisor"));
        var asset = await CreateAsset(dept.Data.Id);

        Auth(await LoginToken(applicant.Data!.EmployeeNo, "123456"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.Data!.BizType,
            AssetId = asset.Data!.Id,
            Reason = "测试管理员角色分支"
        });

        flow.Data!.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_SupervisorRole");

        Auth(await LoginToken(warehouse.Data!.EmployeeNo, "123456"));
        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "同意" });

        approved.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public void Invalid_condition_does_not_default_to_true()
    {
        var process = BpmnParser.Parse(InvalidConditionGatewayBpmn());
        var flow = new TestFlow();

        var act = () => BpmnEngine.Start(flow, process);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*无法识别*");
    }

    [Fact]
    public async Task Supervisor_node_allows_only_applicant_supervisor()
    {
        await Login();
        var supervisorRole = await Role("supervisor");
        var dept = await CreateDepartment("主管解析部门");
        var employeeRole = await Role("employee");
        var supervisor = await CreateUser("直属主管", supervisorRole.Id, dept.Data!.Id);
        var otherSupervisor = await CreateUser("其他主管", supervisorRole.Id, dept.Data!.Id);
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{dept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = dept.Data.Name,
            ManagerId = supervisor.Data!.Id,
            IsActive = true
        });
        var applicant = await CreateUser("有上级员工", employeeRole.Id, dept.Data!.Id, supervisor.Data!.Id);
        var workflow = await CreateWorkflow("supervisor", SupervisorBpmn());
        var asset = await CreateAsset(dept.Data.Id);

        Auth(await LoginToken(applicant.Data!.EmployeeNo, "123456"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.Data!.BizType,
            AssetId = asset.Data!.Id,
            Reason = "测试直属主管解析"
        });

        Auth(await LoginToken(otherSupervisor.Data!.EmployeeNo, "123456"));
        var denied = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { Opinion = "不应通过" });
        var deniedBody = await denied.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        deniedBody!.Code.Should().NotBe(0);

        Auth(await LoginToken(supervisor.Data.EmployeeNo, "123456"));
        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "同意" });
        approved.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Workflow_save_rejects_user_task_with_multiple_outgoing_flows()
    {
        await Login();
        var res = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = "非法多出边流程",
            BizType = Unique("invalid"),
            BpmnXml = UserTaskMultiOutgoingBpmn()
        });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        body!.Code.Should().NotBe(0);
        body.Message.Should().Contain("出边");
    }

    private async Task Login()
    {
        Auth(await LoginToken("1001", "123456"));
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo, password });
        return body.Data!.Token;
    }

    private async Task<RoleDto> Role(string code)
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        return roles!.Data!.Items.Single(x => x.Code == code);
    }

    private Task<ApiResult<DepartmentNodeDto>> CreateDepartment(string name)
        => Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = $"{name}{Guid.NewGuid():N}"[..20]
        });

    private Task<ApiResult<UserDto>> CreateUser(string name, int roleId, int? departmentId = null, int? supervisorId = null)
    {
        var employeeNo = Unique("U");
        return Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = $"{name}{employeeNo}",
            Password = "123456",
            DepartmentId = departmentId,
            SupervisorId = supervisorId,
            RoleIds = new[] { roleId }
        });
    }

    private Task<ApiResult<WorkflowDto>> CreateWorkflow(string prefix, string bpmnXml)
        => PostOk<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = $"{prefix}测试流程",
            BizType = Unique(prefix),
            BpmnXml = bpmnXml
        });

    private async Task<ApiResult<AssetDto>> CreateAsset(int? departmentId = null)
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
        });
        return await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "BPMN测试资产",
            CategoryId = child.Data!.Id,
            DepartmentId = departmentId,
        });
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

    private async Task<T> PostOk<T>(string url, object body) where T : class
    {
        var result = await Post<T>(url, body);
        var code = (int?)result.GetType().GetProperty("Code")?.GetValue(result);
        var message = (string?)result.GetType().GetProperty("Message")?.GetValue(result);
        code.Should().Be(0, message);
        return result;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static string DepartmentGatewayBpmn(string dept, string matchedTaskId, string defaultTaskId) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Dept" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway_Dept" />
    <bpmn:userTask id="{{matchedTaskId}}" name="部门匹配审批" camunda:assignee="系统管理员" />
    <bpmn:userTask id="{{defaultTaskId}}" name="默认审批" camunda:assignee="系统管理员" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Gateway_Dept" />
    <bpmn:sequenceFlow id="Flow_Matched" sourceRef="Gateway_Dept" targetRef="{{matchedTaskId}}">
      <bpmn:conditionExpression>${applicantDept} == "{{dept}}"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_Default" sourceRef="Gateway_Dept" targetRef="{{defaultTaskId}}" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="{{matchedTaskId}}" targetRef="End" />
    <bpmn:sequenceFlow id="Flow_3" sourceRef="{{defaultTaskId}}" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string ApplicantRoleGatewayBpmn(string role, string matchedTaskId, string defaultTaskId) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Role" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway_Role" />
    <bpmn:userTask id="{{matchedTaskId}}" name="角色匹配审批" camunda:assignee="系统管理员" />
    <bpmn:userTask id="{{defaultTaskId}}" name="默认审批" camunda:assignee="系统管理员" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Gateway_Role" />
    <bpmn:sequenceFlow id="Flow_Matched" sourceRef="Gateway_Role" targetRef="{{matchedTaskId}}">
      <bpmn:conditionExpression>${applicantRole} == "{{role}}"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_Default" sourceRef="Gateway_Role" targetRef="{{defaultTaskId}}" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="{{matchedTaskId}}" targetRef="End" />
    <bpmn:sequenceFlow id="Flow_3" sourceRef="{{defaultTaskId}}" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string ApplicantRoleWorkflowBpmn(string roleCode, string matchedAssignee, string defaultRoleCode) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_RoleApproval" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway_Role" />
    <bpmn:userTask id="Task_SupervisorRole" name="角色分支审批" camunda:assignee="{{matchedAssignee}}" />
    <bpmn:userTask id="Task_DefaultRole" name="默认角色审批" camunda:candidateGroups="{{defaultRoleCode}}" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Gateway_Role" />
    <bpmn:sequenceFlow id="Flow_Matched" sourceRef="Gateway_Role" targetRef="Task_SupervisorRole">
      <bpmn:conditionExpression>${applicantRole} == "{{roleCode}}"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_Default" sourceRef="Gateway_Role" targetRef="Task_DefaultRole" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_SupervisorRole" targetRef="End" />
    <bpmn:sequenceFlow id="Flow_3" sourceRef="Task_DefaultRole" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string ApplicantRoleToRoleWorkflowBpmn(string roleCode, string matchedRoleCode, string defaultRoleCode) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_RoleApproval" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway_Role" />
    <bpmn:userTask id="Task_SupervisorRole" name="角色分支审批" camunda:candidateGroups="{{matchedRoleCode}}" />
    <bpmn:userTask id="Task_DefaultRole" name="默认角色审批" camunda:candidateGroups="{{defaultRoleCode}}" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Gateway_Role" />
    <bpmn:sequenceFlow id="Flow_Matched" sourceRef="Gateway_Role" targetRef="Task_SupervisorRole">
      <bpmn:conditionExpression>${applicantRole} == "{{roleCode}}"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_Default" sourceRef="Gateway_Role" targetRef="Task_DefaultRole" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_SupervisorRole" targetRef="End" />
    <bpmn:sequenceFlow id="Flow_3" sourceRef="Task_DefaultRole" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string InvalidConditionGatewayBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Invalid" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway" />
    <bpmn:userTask id="Task_Invalid" name="错误条件审批" camunda:assignee="系统管理员" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Gateway" />
    <bpmn:sequenceFlow id="Flow_Bad" sourceRef="Gateway" targetRef="Task_Invalid">
      <bpmn:conditionExpression>${unknown} == "x"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_Invalid" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string SupervisorBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Supervisor" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:userTask id="Task_Supervisor" name="直属主管审批" camunda:assignee="supervisor" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Task_Supervisor" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_Supervisor" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string UserTaskMultiOutgoingBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_MultiOutgoing" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:userTask id="Task_Approve" name="审批" camunda:assignee="系统管理员" />
    <bpmn:endEvent id="End_1" />
    <bpmn:endEvent id="End_2" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Task_Approve" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_Approve" targetRef="End_1" />
    <bpmn:sequenceFlow id="Flow_3" sourceRef="Task_Approve" targetRef="End_2" />
  </bpmn:process>
</bpmn:definitions>
""";

    private sealed class TestFlow : IBpmnFlowInstance
    {
        public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();
        public List<string> CurrentNodeIds { get; set; } = new();
        public string Status { get; set; } = "pending";
        public string? ApplicantDept { get; init; }
        public Dictionary<string, string>? Context { get; set; }
    }

    private const string OrganizationApprovalBpmn = """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Organization" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway_Section" />
    <bpmn:userTask id="Task_SectionManager" camunda:assignee="sectionManager" />
    <bpmn:exclusiveGateway id="Gateway_Department" />
    <bpmn:userTask id="Task_DepartmentManager" camunda:assignee="departmentManager" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Gateway_Section" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Gateway_Section" targetRef="Task_SectionManager"><bpmn:conditionExpression>${requiresSectionApproval} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_3" sourceRef="Gateway_Section" targetRef="Gateway_Department" />
    <bpmn:sequenceFlow id="Flow_4" sourceRef="Task_SectionManager" targetRef="Gateway_Department" />
    <bpmn:sequenceFlow id="Flow_5" sourceRef="Gateway_Department" targetRef="Task_DepartmentManager"><bpmn:conditionExpression>${requiresDepartmentApproval} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="Flow_6" sourceRef="Gateway_Department" targetRef="End" />
    <bpmn:sequenceFlow id="Flow_7" sourceRef="Task_DepartmentManager" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";
}
