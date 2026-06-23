using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

public class ApprovalSignApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public ApprovalSignApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Multi_sign_task_requires_all_configured_users()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("会签甲");
        var userB = await CreateApprover("会签乙");
        var workflow = await CreateWorkflow("countersign", MultiSignBpmn($"{userA.Name},{userB.Name}"));
        var asset = await CreateAsset();

        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "测试会签"
        });

        flow.Code.Should().Be(0, flow.Message);
        flow.Data!.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_CounterSign");
        flow.Data.BpmnTokens["Task_CounterSign"].SignStates.Should().ContainKeys(userA.Name, userB.Name);

        Auth(await LoginToken(userA.EmployeeNo, "123456"));
        var first = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "甲同意" });

        first.Data!.Status.Should().Be("pending");
        first.Data.CurrentNodeIds.Should().Contain("Task_CounterSign");
        first.Data.BpmnTokens["Task_CounterSign"].SignStates![userA.Name].Should().BeTrue();
        first.Data.BpmnTokens["Task_CounterSign"].SignStates![userB.Name].Should().BeFalse();

        Auth(await LoginToken(userB.EmployeeNo, "123456"));
        var second = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "乙同意" });

        second.Data!.Status.Should().Be("approved");
        second.Data.CurrentNodeIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_sign_requires_added_user_before_advancing()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("主审人");
        var userB = await CreateApprover("加签人");
        var workflow = await CreateWorkflow("addsign", SingleUserBpmn(userA.Name));
        var asset = await CreateAsset();

        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "测试加签"
        });

        flow.Code.Should().Be(0, flow.Message);
        Auth(await LoginToken(userA.EmployeeNo, "123456"));
        var addSign = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/add-sign",
            new AddSignRequest { Who = userB.Name });

        addSign.Data!.BpmnTokens["Task_Approve"].SignStates.Should().ContainKeys(userA.Name, userB.Name);

        var first = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "主审同意" });

        first.Data!.Status.Should().Be("pending");
        first.Data.CurrentNodeIds.Should().Contain("Task_Approve");

        Auth(await LoginToken(userB.EmployeeNo, "123456"));
        var second = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "加签同意" });

        second.Data!.Status.Should().Be("approved");
    }

    private async Task<UserDto> CreateApprover(string name)
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var employeeRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");
        var employeeNo = $"SG{Guid.NewGuid():N}"[..10];

        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = $"{name}{employeeNo}",
            Password = "123456",
            RoleIds = new[] { employeeRole.Id }
        });

        return user.Data!;
    }

    private async Task<WorkflowDto> CreateWorkflow(string prefix, string bpmnXml)
    {
        var workflow = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = $"{prefix}测试流程",
            BizType = $"{prefix}_{Guid.NewGuid():N}"[..20],
            BpmnXml = bpmnXml
        });
        return workflow.Data!;
    }

    private async Task<AssetDto> CreateAsset()
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("SG")
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = Unique("SN")
        });
        var asset = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "签核测试资产",
            CategoryId = child.Data!.Id,
        });
        return asset.Data!;
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo, password });
        return body.Data!.Token;
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

    private static string SingleUserBpmn(string userName) => $"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xmlns:camunda="http://camunda.org/schema/1.0/bpmn" id="Defs" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="true">
    <bpmn:startEvent id="Start"><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:userTask id="Task_Approve" name="审批" camunda:assignee="{userName}"><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing></bpmn:userTask>
    <bpmn:endEvent id="End"><bpmn:incoming>Flow_2</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Task_Approve" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_Approve" targetRef="End" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="Diagram"><bpmndi:BPMNPlane id="Plane" bpmnElement="Process_1">
    <bpmndi:BPMNShape id="Start_di" bpmnElement="Start"><dc:Bounds x="100" y="100" width="36" height="36" /></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id="Task_Approve_di" bpmnElement="Task_Approve"><dc:Bounds x="180" y="80" width="100" height="80" /></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id="End_di" bpmnElement="End"><dc:Bounds x="330" y="100" width="36" height="36" /></bpmndi:BPMNShape>
    <bpmndi:BPMNEdge id="Flow_1_di" bpmnElement="Flow_1"><di:waypoint x="136" y="118" /><di:waypoint x="180" y="120" /></bpmndi:BPMNEdge>
    <bpmndi:BPMNEdge id="Flow_2_di" bpmnElement="Flow_2"><di:waypoint x="280" y="120" /><di:waypoint x="330" y="118" /></bpmndi:BPMNEdge>
  </bpmndi:BPMNPlane></bpmndi:BPMNDiagram>
</bpmn:definitions>
""";

    private static string MultiSignBpmn(string userNames) => $"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xmlns:camunda="http://camunda.org/schema/1.0/bpmn" id="Defs" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="true">
    <bpmn:startEvent id="Start"><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:userTask id="Task_CounterSign" name="会签审批" camunda:candidateUsers="{userNames}" camunda:approvalMode="all"><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing></bpmn:userTask>
    <bpmn:endEvent id="End"><bpmn:incoming>Flow_2</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Task_CounterSign" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_CounterSign" targetRef="End" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="Diagram"><bpmndi:BPMNPlane id="Plane" bpmnElement="Process_1">
    <bpmndi:BPMNShape id="Start_di" bpmnElement="Start"><dc:Bounds x="100" y="100" width="36" height="36" /></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id="Task_CounterSign_di" bpmnElement="Task_CounterSign"><dc:Bounds x="180" y="80" width="100" height="80" /></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id="End_di" bpmnElement="End"><dc:Bounds x="330" y="100" width="36" height="36" /></bpmndi:BPMNShape>
    <bpmndi:BPMNEdge id="Flow_1_di" bpmnElement="Flow_1"><di:waypoint x="136" y="118" /><di:waypoint x="180" y="120" /></bpmndi:BPMNEdge>
    <bpmndi:BPMNEdge id="Flow_2_di" bpmnElement="Flow_2"><di:waypoint x="280" y="120" /><di:waypoint x="330" y="118" /></bpmndi:BPMNEdge>
  </bpmndi:BPMNPlane></bpmndi:BPMNDiagram>
</bpmn:definitions>
""";
}
