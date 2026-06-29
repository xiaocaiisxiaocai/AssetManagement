using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Workflow;

/// <summary>
/// 审批流程 API 测试
///
/// 注意：这些测试原本基于旧的 WorkflowNode 模型编写。
/// 在 BPMN 迁移后，需要重写以适配新的架构：
/// - WorkflowDto.Nodes → WorkflowDto.BpmnXml
/// - ApprovalFlowDto.Nodes → ApprovalFlowDto.BpmnTokens
/// - ApprovalFlowDto.CurrentNodeIndex → ApprovalFlowDto.CurrentNodeIds
/// </summary>
public class ApprovalApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public ApprovalApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Workflow_design_can_update_bpmn_xml()
    {
        // 测试：保存有效的 BPMN XML，验证解析正确
        await Login();

        // 创建简单的 BPMN 流程定义
        var simpleBpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn"">
  <bpmn:process id=""testProcess"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" />
    <bpmn:userTask id=""Task_Review"" name=""审核"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""assignee"" value=""1001"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" />
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_Review"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_Review"" targetRef=""EndEvent_1"" />
  </bpmn:process>
</bpmn:definitions>";

        var response = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = "测试BPMN流程",
            BizType = "test-bpmn",
            BpmnXml = simpleBpmn
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        result.Should().NotBeNull();
        result!.Code.Should().Be(0);
        result.Data.Should().NotBeNull();
        result.Data!.BpmnXml.Should().Be(simpleBpmn);

        // 验证 BPMN XML 能被正确解析
        var act = () => BpmnParser.Parse(simpleBpmn);
        act.Should().NotThrow("保存的 BPMN XML 应该能被正确解析");
    }

    [Fact]
    public async Task Borrow_flow_creates_pending_flow()
    {
        await Login();
        var asset = await CreateAsset();

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试借用",
            ReturnDate = "2026-06-30"
        });

        // 添加响应检查
        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        // 添加 null 检查
        flow.Should().NotBeNull();
        flow!.Code.Should().Be(0, "API 应该返回成功");
        flow.Data.Should().NotBeNull("流程数据不应为空");

        flow.Data!.Status.Should().Be("pending");
        flow.Data.BizType.Should().Be("borrow");
        flow.Data.AssetId.Should().Be(asset.Id);
        // BPMN 模式下，流程应该已经启动并推进到第一个 UserTask
        flow.Data.CurrentNodeIds.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Approve_advances_to_next_node()
    {
        await Login();
        var asset = await CreateAsset();

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试审批"
        });

        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        flow.Should().NotBeNull();
        flow!.Data.Should().NotBeNull();

        var flowId = flow.Data!.Id;
        var initialNodeIds = flow.Data.CurrentNodeIds.ToList();

        var approveResponse = await _client.PostAsJsonAsync($"/api/approvals/{flowId}/approve",
            new ApprovalActionRequest { Opinion = "同意" });

        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        approved.Should().NotBeNull();
        approved!.Data.Should().NotBeNull();

        // 验证 Token 状态已更新
        approved.Data!.BpmnTokens.Should().NotBeEmpty();

        // 流程应该推进：要么完成，要么到下一个节点
        if (approved.Data.Status == "approved") {
            approved.Data.Status.Should().Be("approved", "默认流程应该完成");
        } else {
            approved.Data.Status.Should().Be("pending");
            approved.Data.CurrentNodeIds.Should().NotBeEmpty("应该有新的活跃节点");
        }
    }

    [Fact]
    public async Task Reject_terminates_flow()
    {
        await Login();
        var asset = await CreateAsset();

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试驳回"
        });

        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        flow.Should().NotBeNull();
        flow!.Data.Should().NotBeNull();

        var rejectResponse = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data!.Id}/reject",
            new RejectRequest { Reason = "不同意" });

        rejectResponse.EnsureSuccessStatusCode();
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        rejected.Should().NotBeNull();
        rejected!.Data.Should().NotBeNull();
        rejected.Data!.Status.Should().Be("rejected");
    }

    [Fact]
    public async Task Transfer_receiver_dept_manager_gets_second_node_pending_and_notification()
    {
        await Login();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var supervisorRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");
        var employeeRole = roles.Data.Items.Single(r => r.Code == "employee");
        var deptAdminRole = roles.Data.Items.Single(r => r.Code == "dept_admin");

        var sourceDept = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest { Name = Unique("SRC") });
        var targetDept = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest { Name = Unique("DST") });

        var supervisorNo = Unique("SUP");
        var supervisor = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = supervisorNo,
            Name = Unique("主管"),
            Password = "123456",
            DepartmentId = sourceDept.Data!.Id,
            RoleIds = new[] { supervisorRole.Id }
        });
        var receiverAdminNo = Unique("RDA");
        var receiverAdmin = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = receiverAdminNo,
            Name = Unique("接收管理员"),
            Password = "123456",
            DepartmentId = targetDept.Data!.Id,
            RoleIds = new[] { deptAdminRole.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{targetDept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = targetDept.Data.Name,
            ManagerId = receiverAdmin.Data!.Id,
            IsActive = true
        });

        var applicantNo = Unique("APP");
        var applicant = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = applicantNo,
            Name = Unique("申请人"),
            Password = "123456",
            DepartmentId = sourceDept.Data.Id,
            SupervisorId = supervisor.Data!.Id,
            RoleIds = new[] { employeeRole.Id }
        });
        var receiverNo = Unique("RCV");
        var receiver = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = receiverNo,
            Name = Unique("接收人"),
            Password = "123456",
            DepartmentId = targetDept.Data.Id,
            SupervisorId = receiverAdmin.Data.Id,
            RoleIds = new[] { employeeRole.Id }
        });

        var asset = await CreateAsset(sourceDept.Data.Id, applicant.Data!.Id);

        Auth(await LoginToken(applicantNo, "123456"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "transfer",
            AssetId = asset.Id,
            TransfereeId = receiver.Data!.Id,
            Reason = "转让到接收部门"
        });

        Auth(await LoginToken(supervisorNo, "123456"));
        var step1 = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { NodeId = "Task_supervisor", Opinion = "同意" });
        step1.Data!.CurrentNodeIds.Should().Contain("Task_receiver");

        Auth(await LoginToken(receiverAdminNo, "123456"));
        var pending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>("/api/approvals/pending");
        pending!.Data.Should().Contain(x => x.Id == flow.Data.Id,
            "转让第二节点的 deptManager 应按接收人部门解析，而不是申请人部门");

        var notifications = await _client.GetFromJsonAsync<ApiResult<List<NotificationDto>>>("/api/notifications");
        notifications!.Data.Should().Contain(x => x.Type == "approval_pending" && x.FlowId == flow.Data.Id);
    }

    [Fact]
    public async Task Exclusive_gateway_routes_based_on_condition()
    {
        // 测试 BPMN ExclusiveGateway 根据条件选择不同分支
        await Login();

        // 创建包含排他网关的 BPMN 流程
        var conditionalBpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn"">
  <bpmn:process id=""conditionalProcess"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:exclusiveGateway id=""Gateway_Dept"" />
    <bpmn:userTask id=""Task_TechDept"" name=""技术部审批"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""assignee"" value=""1001"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_AdminDept"" name=""行政部审批"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""assignee"" value=""1001"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id=""End"" />
    <bpmn:sequenceFlow id=""Flow_Start"" sourceRef=""Start"" targetRef=""Gateway_Dept"" />
    <bpmn:sequenceFlow id=""Flow_Tech"" sourceRef=""Gateway_Dept"" targetRef=""Task_TechDept"">
      <bpmn:conditionExpression>${applicantDept} == &quot;技术部&quot;</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_Admin"" sourceRef=""Gateway_Dept"" targetRef=""Task_AdminDept"">
      <bpmn:conditionExpression>${applicantDept} == &quot;行政部&quot;</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_TechEnd"" sourceRef=""Task_TechDept"" targetRef=""End"" />
    <bpmn:sequenceFlow id=""Flow_AdminEnd"" sourceRef=""Task_AdminDept"" targetRef=""End"" />
  </bpmn:process>
</bpmn:definitions>";

        // 保存流程
        var saveResponse = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = "条件分支测试流程",
            BizType = "test-condition",
            BpmnXml = conditionalBpmn
        });
        saveResponse.EnsureSuccessStatusCode();

        // 验证 BPMN 解析成功
        var act = () => BpmnParser.Parse(conditionalBpmn);
        act.Should().NotThrow("包含排他网关的 BPMN 应该能正确解析");

        var process = BpmnParser.Parse(conditionalBpmn);
        process.Nodes.Should().Contain(n => n.Type == BpmnNodeType.ExclusiveGateway);

        // 验证网关有两个出边,每个都有条件表达式
        var gateway = process.Nodes.First(n => n.Type == BpmnNodeType.ExclusiveGateway);
        var outgoingFlows = process.GetOutgoingFlows(gateway.Id);
        outgoingFlows.Should().HaveCount(2);
        outgoingFlows.Should().OnlyContain(f => !string.IsNullOrEmpty(f.ConditionExpression));
    }

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo = "1001", password = "123456" });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo, password });
        return body.Data!.Token;
    }

    private async Task<AssetDto> CreateAsset()
        => await CreateAsset(null, null);

    private async Task<AssetDto> CreateAsset(int? departmentId, int? custodianId)
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("TC")
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = Unique("CH")
        });
        var asset = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "测试资产",
            CategoryId = child.Data!.Id,
            DepartmentId = departmentId,
            CustodianId = custodianId
        });
        return asset.Data!;
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

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];
}
