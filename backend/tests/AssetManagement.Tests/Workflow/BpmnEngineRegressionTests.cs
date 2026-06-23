using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
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
        var applicant = await CreateUser("有上级员工", employeeRole.Id, dept.Data!.Id, supervisor.Data!.Id);
        var workflow = await CreateWorkflow("supervisor", SupervisorBpmn());
        var asset = await CreateAsset();

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
        => Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = $"{prefix}测试流程",
            BizType = Unique(prefix),
            BpmnXml = bpmnXml
        });

    private async Task<ApiResult<AssetDto>> CreateAsset()
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("BC")
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = Unique("BL")
        });
        return await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "BPMN测试资产",
            CategoryId = child.Data!.Id,
        });
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

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
    }
}
