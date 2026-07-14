using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Workflow;

/// <summary>
/// 并行节点待办契约：流程保留全部活动节点，当前用户只能操作自己的节点。
/// </summary>
public class ParallelApprovalWorkItemTests : IClassFixture<TestWebAppFactory>
{
    private const string NodeA = "Task_A";
    private const string NodeB = "Task_B";

    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public ParallelApprovalWorkItemTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Asset_pending_exposes_only_nodes_actionable_by_current_user()
    {
        await LoginAdmin();
        var approverA = await CreateApprover("APRA", "资产并行审批人A");
        var approverB = await CreateApprover("APRB", "资产并行审批人B");
        var workflow = await CreateWorkflow(
            $"parallel_asset_{Guid.NewGuid():N}"[..40],
            ParallelBpmn(approverA.Id, approverB.Id));
        var asset = await CreateAsset("并行审批资产");

        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "验证并行节点个人待办隔离"
        });
        flow.Data!.CurrentNodeIds.Should().BeEquivalentTo(NodeA, NodeB);

        Auth(await LoginToken(approverA.EmployeeNo, "123456"));
        var pending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>("/api/approvals/pending");
        var workItem = pending!.Data.Should().ContainSingle(x => x.Id == flow.Data.Id).Which;
        workItem.CurrentNodeIds.Should().BeEquivalentTo(NodeA, NodeB);
        workItem.ActionableNodeIds.Should().ContainSingle().Which.Should().Be(NodeA);

        var deniedResponse = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { NodeId = NodeB, Opinion = "越权处理B节点" });
        var denied = await deniedResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        denied!.Code.Should().Be(4016);

        var approvedA = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { NodeId = NodeA, Opinion = "A节点通过" });
        approvedA.Code.Should().Be(0, approvedA.Message);
        approvedA.Data!.Status.Should().Be("pending");
        approvedA.Data.CurrentNodeIds.Should().ContainSingle().Which.Should().Be(NodeB);

        Auth(await LoginToken(approverB.EmployeeNo, "123456"));
        var pendingB = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>("/api/approvals/pending");
        var workItemB = pendingB!.Data.Should().ContainSingle(x => x.Id == flow.Data.Id).Which;
        workItemB.ActionableNodeIds.Should().ContainSingle().Which.Should().Be(NodeB);

        var approvedB = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { NodeId = NodeB, Opinion = "B节点通过" });
        approvedB.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Material_pending_requires_explicit_actionable_parallel_node()
    {
        await LoginAdmin();
        await SetMaterialApprovalSwitch(true);
        var approverA = await CreateApprover("MPRA", "料件并行审批人A");
        var approverB = await CreateApprover("MPRB", "料件并行审批人B");
        var originalBpmn = await ReplaceActiveMaterialWorkflowBpmn(
            ParallelBpmn(approverA.Id, approverB.Id));

        try
        {
            var project = await CreateProject("料件并行审批项目");
            var transferee = await CreateEmployee("MTR", "料件并行审批接收人");
            var material = await CreateMaterial(project.Id, "并行审批料件");
            var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
            {
                MaterialId = material.Id,
                TransfereeId = transferee.Id,
                Reason = "验证料件并行节点个人待办隔离"
            });
            flow.Data!.CurrentNodeIds.Should().BeEquivalentTo(NodeA, NodeB);

            Auth(await LoginToken(approverA.EmployeeNo, "123456"));
            var pending = await _client.GetFromJsonAsync<ApiResult<List<MaterialFlowDto>>>(
                "/api/material-flows/pending");
            var workItem = pending!.Data.Should().ContainSingle(x => x.Id == flow.Data.Id).Which;
            workItem.CurrentNodeIds.Should().BeEquivalentTo(NodeA, NodeB);
            workItem.ActionableNodeIds.Should().ContainSingle().Which.Should().Be(NodeA);

            var deniedResponse = await _client.PostAsJsonAsync($"/api/material-flows/{flow.Data.Id}/approve",
                new MaterialApprovalRequest { NodeId = NodeB, Opinion = "越权处理B节点" });
            var denied = await deniedResponse.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();
            denied!.Code.Should().Be(4016);

            var approvedA = await Post<ApiResult<MaterialFlowDto>>($"/api/material-flows/{flow.Data.Id}/approve",
                new MaterialApprovalRequest { NodeId = NodeA, Opinion = "A节点通过" });
            approvedA.Code.Should().Be(0, approvedA.Message);
            approvedA.Data!.Status.Should().Be("pending");
            approvedA.Data.CurrentNodeIds.Should().ContainSingle().Which.Should().Be(NodeB);

            Auth(await LoginToken(approverB.EmployeeNo, "123456"));
            var pendingB = await _client.GetFromJsonAsync<ApiResult<List<MaterialFlowDto>>>(
                "/api/material-flows/pending");
            var workItemB = pendingB!.Data.Should().ContainSingle(x => x.Id == flow.Data.Id).Which;
            workItemB.ActionableNodeIds.Should().ContainSingle().Which.Should().Be(NodeB);

            var approvedB = await Post<ApiResult<MaterialFlowDto>>($"/api/material-flows/{flow.Data.Id}/approve",
                new MaterialApprovalRequest { NodeId = NodeB, Opinion = "B节点通过" });
            approvedB.Data!.Status.Should().Be("approved");
        }
        finally
        {
            await ReplaceActiveMaterialWorkflowBpmn(originalBpmn);
        }
    }

    [Fact]
    public async Task Legacy_numeric_assignee_is_rejected_when_user_id_and_employee_number_collide()
    {
        await LoginAdmin();
        var idOwner = await CreateEmployee("LEG", "历史ID审批人");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                EmployeeNo = idOwner.Id.ToString(),
                Name = $"历史工号碰撞审批人{idOwner.Id}",
                PasswordHash = "not-used",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
        var workflow = await CreateWorkflow(
            $"legacy_collision_{Guid.NewGuid():N}"[..40],
            SingleTaskBpmn(idOwner.Id.ToString()));
        var asset = await CreateAsset("历史审批人歧义测试资产");
        workflow.Should().NotBeNull();
        asset.Should().NotBeNull();

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.BizType,
            AssetId = asset.Id,
            Reason = "歧义配置必须拒绝发起"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        body!.Code.Should().Be(4051);
        body.Message.Should().Contain("审批人配置存在歧义");
    }

    private async Task<WorkflowDto> CreateWorkflow(string bizType, string bpmnXml)
        => (await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = $"审批回归流程-{bizType}",
            BizType = bizType,
            BpmnXml = bpmnXml
        })).Data!;

    private async Task<AssetDto> CreateAsset(string name)
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
        return (await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = name,
            CategoryId = child.Data!.Id
        })).Data!;
    }

    private async Task<TestProjectDto> CreateProject(string name)
        => (await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = $"TP-{Guid.NewGuid():N}"[..20],
            FollowUpIntervalDays = 14,
            Name = name,
            OwnerId = 1,
            PlannedFinishDate = new DateTime(2026, 8, 31),
            ProgressCode = "testing",
            ProjectTypeCode = "prototype",
            StartDate = new DateTime(2026, 7, 1)
        })).Data!;

    private async Task<TestMaterialDto> CreateMaterial(int projectId, string name)
        => (await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = name,
            ProjectId = projectId,
            CustodianId = 1
        })).Data!;

    private Task<UserDto> CreateApprover(string prefix, string name)
        => CreateUser(prefix, name, "admin");

    private Task<UserDto> CreateEmployee(string prefix, string name)
        => CreateUser(prefix, name, "employee");

    private async Task<UserDto> CreateUser(string prefix, string name, string roleCode)
    {
        var employeeNo = $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        return (await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = $"{name}{employeeNo[^4..]}",
            Password = "123456",
            RoleIds = new[] { (await Role(roleCode)).Id }
        })).Data!;
    }

    private async Task<RoleDto> Role(string code)
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        return roles!.Data!.Items.Single(x => x.Code == code);
    }

    private async Task SetMaterialApprovalSwitch(bool enabled)
    {
        var response = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "material.transfer.approval.enabled",
                Value = enabled ? "true" : "false",
                Description = "是否启用测试料件转移审批(false=直接转移)"
            }
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> ReplaceActiveMaterialWorkflowBpmn(string bpmnXml)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.Workflows.AsTracking()
            .SingleAsync(x => x.BizType == "material_transfer" && x.IsActive);
        var original = workflow.BpmnXml!;
        workflow.BpmnXml = bpmnXml;
        await db.SaveChangesAsync();
        return original;
    }

    private async Task LoginAdmin()
        => Auth(await LoginToken("1001", "123456"));

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
        => (await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo, password })).Data!.Token;

    private async Task<T> Post<T>(string url, object payload)
    {
        var response = await _client.PostAsJsonAsync(url, payload);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static string ParallelBpmn(int userAId, int userBId) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_ParallelApproval" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:parallelGateway id="Fork" />
    <bpmn:userTask id="{{NodeA}}" name="并行审批A" camunda:assignee="user:{{userAId}}" />
    <bpmn:userTask id="{{NodeB}}" name="并行审批B" camunda:assignee="user:{{userBId}}" />
    <bpmn:parallelGateway id="Join" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Fork" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Fork" targetRef="{{NodeA}}" />
    <bpmn:sequenceFlow id="Flow_3" sourceRef="Fork" targetRef="{{NodeB}}" />
    <bpmn:sequenceFlow id="Flow_4" sourceRef="{{NodeA}}" targetRef="Join" />
    <bpmn:sequenceFlow id="Flow_5" sourceRef="{{NodeB}}" targetRef="Join" />
    <bpmn:sequenceFlow id="Flow_6" sourceRef="Join" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string SingleTaskBpmn(string assignee) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_LegacyAssignee" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:userTask id="Task_Approval" name="历史审批节点" camunda:assignee="{{assignee}}" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Task_Approval" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_Approval" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";
}
