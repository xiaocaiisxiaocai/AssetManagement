using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Workflow;

public class ApprovalSignApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public ApprovalSignApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Multi_sign_task_requires_all_configured_users()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("会签甲");
        var userB = await CreateApprover("会签乙");
        var workflow = await CreateWorkflow("countersign", MultiSignBpmn($"{userA.Id},{userB.Id}"));
        var asset = await CreateAsset();

        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "测试会签"
        });

        flow.Code.Should().Be(0, flow.Message);
        flow.Data!.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_CounterSign");
        flow.Data.BpmnTokens["Task_CounterSign"].SignStates.Should().ContainKeys(userA.Id.ToString(), userB.Id.ToString());

        Auth(await LoginToken(userA.EmployeeNo, "TestPass123"));
        var first = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "甲同意" });

        first.Data!.Status.Should().Be("pending");
        first.Data.CurrentNodeIds.Should().Contain("Task_CounterSign");
        first.Data.BpmnTokens["Task_CounterSign"].SignStates![userA.Id.ToString()].Should().BeTrue();
        first.Data.BpmnTokens["Task_CounterSign"].SignStates![userB.Id.ToString()].Should().BeFalse();

        Auth(await LoginToken(userB.EmployeeNo, "TestPass123"));
        var second = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "乙同意" });

        second.Data!.Status.Should().Be("approved");
        second.Data.CurrentNodeIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Later_group_countersign_is_normalized_to_stable_user_ids_after_transition()
    {
        Auth(await LoginToken("1001", "123456"));
        var firstApprover = await CreateApprover("前置审批人");
        var roleCode = $"sg{Guid.NewGuid():N}"[..10];
        var groupRole = (await Post<ApiResult<RoleDto>>("/api/roles", new CreateRoleRequest
        {
            Code = roleCode,
            Name = $"后续会签组{roleCode}"
        })).Data!;
        var approvalPermission = (await _client.GetFromJsonAsync<ApiResult<List<PermissionDto>>>("/api/permissions"))!
            .Data!.Single(permission => permission.Code == "approval:handle");
        var setPermissionsResponse = await _client.PutAsJsonAsync($"/api/roles/{groupRole.Id}/permissions", new
        {
            permissionIds = new[] { approvalPermission.Id }
        });
        var setPermissions = await setPermissionsResponse.Content.ReadFromJsonAsync<ApiResult<RoleDto>>();
        setPermissions!.Code.Should().Be(0, setPermissions.Message);
        var signerA = await CreateApproverWithAdditionalRole("后续会签甲", groupRole.Id);
        var signerB = await CreateApproverWithAdditionalRole("后续会签乙", groupRole.Id);
        var workflow = await CreateWorkflow("latergroup", SequentialGroupSignBpmn(firstApprover.Id, roleCode));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "验证后续角色会签规范化"
        });

        Auth(await LoginToken(firstApprover.EmployeeNo, "TestPass123"));
        var transitioned = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { Opinion = "进入会签" });

        transitioned.Data!.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_Group");
        transitioned.Data.BpmnTokens["Task_Group"].SignStates.Should()
            .ContainKeys(signerA.Id.ToString(), signerB.Id.ToString())
            .And.NotContainKey($"role:{roleCode}");

        Auth(await LoginToken(signerA.EmployeeNo, "TestPass123"));
        var firstSigned = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "甲同意" });
        firstSigned.Data!.Status.Should().Be("pending");
        firstSigned.Data.BpmnTokens["Task_Group"].SignStates![signerA.Id.ToString()].Should().BeTrue();

        Auth(await LoginToken(signerB.EmployeeNo, "TestPass123"));
        var completed = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "乙同意" });
        completed.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Multi_sign_rejection_identifies_rejector_and_keeps_prior_approval()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("先签人");
        var userB = await CreateApprover("驳回人");
        var workflow = await CreateWorkflow("signreject", MultiSignBpmn($"{userA.Id},{userB.Id}"));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "验证会签驳回人展示"
        });

        Auth(await LoginToken(userA.EmployeeNo, "TestPass123"));
        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { Opinion = "先签人同意" });
        Auth(await LoginToken(userB.EmployeeNo, "TestPass123"));
        var rejected = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/reject",
            new RejectRequest { Reason = "资料不完整" });

        var step = rejected.Data!.ProgressSteps.Should().ContainSingle(x => x.NodeId == "Task_CounterSign").Which;
        step.Assignees.Should().Contain(x => x.UserId == userA.Id && x.Status == "completed");
        step.Assignees.Should().Contain(x => x.UserId == userB.Id && x.Status == "rejected");
        step.Opinion.Should().Contain("资料不完整");
    }

    [Fact]
    public async Task Add_sign_requires_added_user_before_advancing()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("主审人");
        var userB = await CreateApprover("加签人");
        var workflow = await CreateWorkflow("addsign", SingleUserBpmn(userA.Id.ToString()));
        var asset = await CreateAsset();

        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "测试加签"
        });

        flow.Code.Should().Be(0, flow.Message);
        var rowVersionBeforeAddSign = await LoadRowVersion(flow.Data!.Id);
        Auth(await LoginToken(userA.EmployeeNo, "TestPass123"));
        var addSign = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/add-sign",
            new AddSignRequest { Who = userB.Id.ToString() });

        addSign.Data!.BpmnTokens["Task_Approve"].SignStates.Should().ContainKeys(userA.Id.ToString(), userB.Id.ToString());
        (await LoadRowVersion(flow.Data.Id)).Should().Be(rowVersionBeforeAddSign + 1u,
            "加签必须推进乐观并发版本，避免并发请求覆盖 BPMN token");

        var first = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "主审同意" });

        first.Data!.Status.Should().Be("pending");
        first.Data.CurrentNodeIds.Should().Contain("Task_Approve");

        Auth(await LoginToken(userB.EmployeeNo, "TestPass123"));
        var notifications = await _client.GetFromJsonAsync<ApiResult<List<NotificationDto>>>("/api/notifications");
        notifications!.Data.Should().Contain(x =>
            x.Type == "approval_pending" && x.FlowId == flow.Data.Id && x.Title.Contains("加签"));
        var pending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>("/api/approvals/pending");
        var addedWorkItem = pending!.Data.Should().ContainSingle(x => x.Id == flow.Data.Id).Which;
        addedWorkItem.ActionableNodeIds.Should().ContainSingle().Which.Should().Be("Task_Approve");
        var second = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "加签同意" });

        second.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Dynamically_added_signer_cannot_add_another_signer()
    {
        Auth(await LoginToken("1001", "123456"));
        var primaryApprover = await CreateApprover("主审人");
        var addedApprover = await CreateApprover("被加签人");
        var nestedApprover = await CreateApprover("二次加签候选人");
        var workflow = await CreateWorkflow("nonestedaddsign", SingleUserBpmn(primaryApprover.Id.ToString()));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "禁止无限加签"
        });

        Auth(await LoginToken(primaryApprover.EmployeeNo, "TestPass123"));
        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/add-sign",
            new AddSignRequest { Who = addedApprover.Id.ToString() });
        var rowVersionBeforeNestedAttempt = await LoadRowVersion(flow.Data.Id);

        Auth(await LoginToken(addedApprover.EmployeeNo, "TestPass123"));
        var deniedResponse = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data.Id}/add-sign",
            new AddSignRequest { Who = nestedApprover.Id.ToString() });
        var denied = await deniedResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        denied!.Code.Should().Be(4057);
        denied.Message.Should().Contain("被加签人不能再次发起加签");
        (await LoadRowVersion(flow.Data.Id)).Should().Be(rowVersionBeforeNestedAttempt,
            "被拒绝的二次加签不能修改流程状态");
    }

    [Fact]
    public async Task Approver_cannot_add_sign_to_self()
    {
        Auth(await LoginToken("1001", "123456"));
        var approver = await CreateApprover("自加签主审人");
        var workflow = await CreateWorkflow("selfaddsign", SingleUserBpmn(approver.Id.ToString()));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "禁止加签自己"
        });

        Auth(await LoginToken(approver.EmployeeNo, "TestPass123"));
        var rowVersionBeforeAttempt = await LoadRowVersion(flow.Data!.Id);
        var deniedResponse = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data.Id}/add-sign",
            new AddSignRequest { Who = approver.Id.ToString() });
        var denied = await deniedResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        denied!.Code.Should().Be(4057);
        denied.Message.Should().Contain("不能加签自己");
        (await LoadRowVersion(flow.Data.Id)).Should().Be(rowVersionBeforeAttempt,
            "被拒绝的自加签不能修改流程状态");
    }

    [Fact]
    public async Task Add_sign_can_be_cancelled_by_its_creator_before_added_user_approves()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("主审人");
        var userB = await CreateApprover("误加签人");
        var workflow = await CreateWorkflow("canceladdsign", SingleUserBpmn(userA.Id.ToString()));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "测试取消加签"
        });

        Auth(await LoginToken(userA.EmployeeNo, "TestPass123"));
        var added = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/add-sign",
            new AddSignRequest { Who = userB.Id.ToString() });
        added.Data!.BpmnTokens["Task_Approve"].AddedSigners![userB.Id.ToString()].Should().Be(userA.Id);

        var cancelled = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/cancel-add-sign",
            new CancelAddSignRequest { Who = userB.Id.ToString() });
        cancelled.Data!.BpmnTokens["Task_Approve"].SignStates.Should().NotContainKey(userB.Id.ToString());
        cancelled.Data.BpmnTokens["Task_Approve"].AddedSigners.Should().NotContainKey(userB.Id.ToString());

        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { Opinion = "取消误加签后通过" });
        approved.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Added_supervisor_cannot_cancel_sign_added_by_another_user()
    {
        Auth(await LoginToken("1001", "123456"));
        var userA = await CreateApprover("加签发起人");
        var userB = await CreateApprover("被加签主管");
        var workflow = await CreateWorkflow("cancelothersign", SingleUserBpmn(userA.Id.ToString()));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "管理员不能取消别人发起的加签"
        });

        Auth(await LoginToken(userA.EmployeeNo, "TestPass123"));
        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/add-sign",
            new AddSignRequest { Who = userB.Id.ToString() });

        Auth(await LoginToken(userB.EmployeeNo, "TestPass123"));
        var denied = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data.Id}/cancel-add-sign",
            new CancelAddSignRequest { Who = userB.Id.ToString() });
        var deniedBody = await denied.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        deniedBody!.Code.Should().Be(4031);

        Auth(await LoginToken(userA.EmployeeNo, "TestPass123"));
        var cancelled = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/cancel-add-sign",
            new CancelAddSignRequest { Who = userB.Id.ToString() });
        cancelled.Data!.BpmnTokens["Task_Approve"].SignStates.Should().NotContainKey(userB.Id.ToString());
    }

    [Fact]
    public async Task Add_sign_rejects_ordinary_employee_and_options_only_return_supervisors()
    {
        Auth(await LoginToken("1001", "123456"));
        var approver = await CreateApprover("合法主审主管");
        var employee = await CreateOrdinaryEmployee("普通加签候选人");
        var workflow = await CreateWorkflow("rejectemployeesign", SingleUserBpmn(approver.Id.ToString()));
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "普通员工不能被加签"
        });

        Auth(await LoginToken(approver.EmployeeNo, "TestPass123"));
        var options = await _client.GetFromJsonAsync<ApiResult<List<UserOptionDto>>>("/api/users/approver-options");
        options!.Data.Should().Contain(x => x.Id == approver.Id);
        options.Data.Should().NotContain(x => x.Id == employee.Id);

        var denied = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data!.Id}/add-sign",
            new AddSignRequest { Who = employee.Id.ToString() });
        var deniedBody = await denied.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        deniedBody!.Code.Should().Be(4057);
        deniedBody.Message.Should().Contain("部门主管");
    }

    private async Task<UserDto> CreateApprover(string name)
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var supervisorRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("加签部门")
        });
        var employeeNo = $"SG{Guid.NewGuid():N}"[..10];

        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = $"{name}{employeeNo}",
            Password = "TestPass123",
            DepartmentId = department.Data!.Id,
            RoleIds = new[] { supervisorRole.Id }
        });

        return user.Data!;
    }

    private async Task<UserDto> CreateOrdinaryEmployee(string name)
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var employeeRole = roles!.Data!.Items.Single(r => r.Code == "employee");
        var employeeNo = $"EM{Guid.NewGuid():N}"[..10];
        return (await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = $"{name}{employeeNo}",
            Password = "TestPass123",
            RoleIds = new[] { employeeRole.Id }
        })).Data!;
    }

    private async Task<UserDto> CreateApproverWithAdditionalRole(string name, int additionalRoleId)
    {
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("会签角色部门")
        });
        var employeeNo = $"GR{Guid.NewGuid():N}"[..10];
        return (await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = $"{name}{employeeNo}",
            Password = "TestPass123",
            DepartmentId = department.Data!.Id,
            RoleIds = new[] { additionalRoleId }
        })).Data!;
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
            CodeSeg = UniqueCodeSeg()
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
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

    private async Task<uint> LoadRowVersion(int flowId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ApprovalFlows
            .Where(x => x.Id == flowId)
            .Select(x => x.RowVersion)
            .SingleAsync();
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

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

    private static string SequentialGroupSignBpmn(int firstApproverId, string roleCode) => $"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="SequentialGroupSign" isExecutable="true">
    <bpmn:startEvent id="Start"/>
    <bpmn:userTask id="Task_First" name="前置审批" camunda:assignee="user:{firstApproverId}"/>
    <bpmn:userTask id="Task_Group" name="角色会签" camunda:candidateGroups="role:{roleCode}" camunda:approvalMode="all"/>
    <bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Task_First"/>
    <bpmn:sequenceFlow id="f2" sourceRef="Task_First" targetRef="Task_Group"/>
    <bpmn:sequenceFlow id="f3" sourceRef="Task_Group" targetRef="End"/>
  </bpmn:process>
</bpmn:definitions>
""";
}
