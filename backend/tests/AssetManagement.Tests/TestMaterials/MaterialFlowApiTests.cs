using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Tests.TestMaterials;

// 每个测试方法开头均显式调用 SetApprovalSwitch 设定开关状态，确保测试方法间无顺序依赖。
// TestWebAppFactory 使用独立 MySQL 数据库(GUID 后缀)，保证本类与其他测试类的数据库完全隔离。
public class MaterialFlowApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;
    public MaterialFlowApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Transfer_with_switch_off_changes_custodian_directly()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("直接转移项目");
        var transferee = await CreateUser("0902", "受让人乙");
        var material = await CreateMaterial(project.Id, "直转样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "测试直接转移"
        });
        flow.Data!.DirectTransfer.Should().BeTrue();
        flow.Data.Status.Should().Be("approved");

        var got = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        got!.Data!.CustodianId.Should().Be(transferee.Id);
    }

    [Fact]
    public async Task Direct_transfer_writes_detail_flow_and_operation_record()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("直转详情项目");
        var transferee = await CreateUser("0920", "直转详情受让人");
        var material = await CreateMaterial(project.Id, "直转详情样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "详情应保留流转历史"
        });

        var detail = await _client.GetFromJsonAsync<ApiResult<TestMaterialDetailDto>>(
            $"/api/test-materials/{material.Id}/detail");

        detail!.Data!.Flows.Should().ContainSingle(x =>
            x.Id == flow.Data!.Id &&
            x.Status == "approved" &&
            x.Transferee == transferee.Name &&
            x.Reason == "详情应保留流转历史");
        detail.Data.Records.Should().ContainSingle(x =>
            x.Action == "direct_transfer" &&
            x.Operator == "系统管理员" &&
            x.Comment!.Contains(transferee.Name));
    }

    [Fact]
    public async Task Transfer_with_switch_on_creates_pending_flow_then_approval_changes_custodian()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("审批转移项目");
        var transferee = await CreateUser("0903", "受让人丙");
        var material = await CreateMaterial(project.Id, "审批样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "测试审批转移"
        });
        flow.Data!.Status.Should().Be("pending");
        flow.Data.DirectTransfer.Should().BeFalse();

        // 发起后保管人尚未变更
        var before = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        before!.Data!.CustodianId.Should().NotBe(transferee.Id);
        before.Data.HasPendingFlow.Should().BeTrue();

        // admin 审批通过(admin 绕过审批人校验)
        var approved = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/approve", new MaterialApprovalRequest { Opinion = "同意" });
        approved.Data!.Status.Should().Be("approved");

        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(transferee.Id);
        after.Data.HasPendingFlow.Should().BeFalse();
    }

    [Fact]
    public async Task Transfer_with_switch_on_uses_active_material_workflow_only()
    {
        await Login();
        await SetApprovalSwitch(true);
        var disabledWorkflowId = await AddDisabledMaterialWorkflow();
        var project = await CreateProject("启用流程优先项目");
        var transferee = await CreateUser("0912", "受让人启用流程");
        var material = await CreateMaterial(project.Id, "启用流程优先样品");

        try
        {
            var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
            {
                MaterialId = material.Id,
                TransfereeId = transferee.Id,
                Reason = "应只匹配启用流程"
            });

            flow.Code.Should().Be(0);
            flow.Data!.Status.Should().Be("pending");
            var usedWorkflowId = await UsedWorkflowId(flow.Data.Id);
            usedWorkflowId.Should().Be(await ActiveMaterialWorkflowId());
        }
        finally
        {
            await DeleteWorkflow(disabledWorkflowId);
        }
    }

    [Fact]
    public async Task Transfer_with_switch_on_rejects_when_material_workflow_is_disabled()
    {
        await Login();
        await SetApprovalSwitch(true);
        await DisableActiveMaterialWorkflow();
        var project = await CreateProject("流程停用项目");
        var transferee = await CreateUser("0913", "受让人流程停用");
        var material = await CreateMaterial(project.Id, "流程停用样品");

        try
        {
            var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
            {
                MaterialId = material.Id,
                TransfereeId = transferee.Id,
                Reason = "停用后不应发起"
            });
            var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

            body!.Code.Should().Be(4057);
            body.Message.Should().Contain("流程已停用");
        }
        finally
        {
            await RestoreActiveMaterialWorkflow();
        }
    }

    [Fact]
    public async Task Project_owner_transfer_routes_to_configured_assignee()
    {
        await Login();
        await SetApprovalSwitch(true);
        var owner = await CreateUser("0910", "项目负责人甲", "supervisor");
        var transferee = await CreateUser("0911", "受让人甲");
        var project = await CreateProject("负责人流转项目", owner.Id);
        var material = await CreateMaterial(project.Id, "负责人审批样品");

        Auth(await LoginToken(owner.EmployeeNo, "123456"));
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "负责人发起流转"
        });

        flow.Data!.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_projectOwnerSpecified");

        var denied = await _client.PostAsJsonAsync(
            $"/api/material-flows/{flow.Data.Id}/approve",
            new MaterialApprovalRequest { Opinion = "非指定人员不应可审批" });
        var deniedBody = await denied.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();
        deniedBody!.Code.Should().Be(4016);

        await Login();
        var pending = await _client.GetFromJsonAsync<ApiResult<List<MaterialFlowDto>>>("/api/material-flows/pending");
        pending!.Data!.Select(x => x.Id).Should().Contain(flow.Data.Id);

        var approved = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/approve",
            new MaterialApprovalRequest { Opinion = "指定人员审批通过" });
        approved.Data!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task Project_owner_transfer_notifies_configured_employee_no_assignee()
    {
        await Login();
        await SetApprovalSwitch(true);
        var owner = await CreateUser("0921", "项目负责人通知", "supervisor");
        var transferee = await CreateUser("0922", "通知接收人");
        var project = await CreateProject("负责人通知项目", owner.Id);
        var material = await CreateMaterial(project.Id, "负责人通知样品");

        Auth(await LoginToken(owner.EmployeeNo, "123456"));
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "应通知工号1001对应的系统管理员"
        });

        await Login();
        var notifications = await _client.GetFromJsonAsync<ApiResult<List<NotificationDto>>>(
            "/api/notifications?unreadOnly=true");

        notifications!.Data.Should().Contain(x =>
            x.FlowId == flow.Data!.Id &&
            x.Type == "material_approval_pending" &&
            x.Title.Contains(material.Name));
    }

    [Fact]
    public async Task Transfer_with_switch_on_reject_keeps_custodian()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("驳回项目");
        var transferee = await CreateUser("0904", "受让人丁");
        var material = await CreateMaterial(project.Id, "驳回样品");
        var originalCustodian = material.CustodianId;

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id, TransfereeId = transferee.Id, Reason = "将被驳回"
        });

        var rejected = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data!.Id}/reject", new MaterialRejectRequest { Reason = "不同意" });
        rejected.Data!.Status.Should().Be("rejected");

        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(originalCustodian);
    }

    [Fact]
    public async Task Returned_material_cannot_start_transfer()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("退回禁止转移项目");
        var transferee = await CreateUser("0905", "受让人戊");
        var material = await CreateMaterial(project.Id, "已退回样品");
        await Post<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}/return", new { });

        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "不应允许转移"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(4098);
    }

    [Fact]
    public async Task Approved_flow_cannot_be_approved_again()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("重复审批项目");
        var transferee = await CreateUser("0906", "受让人己");
        var material = await CreateMaterial(project.Id, "重复审批样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id, TransfereeId = transferee.Id, Reason = "发起审批"
        });
        await Post<ApiResult<MaterialFlowDto>>($"/api/material-flows/{flow.Data!.Id}/approve",
            new MaterialApprovalRequest { Opinion = "同意" });

        var response = await _client.PostAsJsonAsync(
            $"/api/material-flows/{flow.Data.Id}/approve",
            new MaterialApprovalRequest { Opinion = "重复审批" });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(4013, "已通过的流转单不应允许重复审批");
    }

    [Fact]
    public async Task Approved_flow_cannot_be_rejected()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("禁止驳回已通过项目");
        var transferee = await CreateUser("0907", "受让人庚");
        var material = await CreateMaterial(project.Id, "已通过样品");

        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id, TransfereeId = transferee.Id, Reason = "发起审批"
        });
        await Post<ApiResult<MaterialFlowDto>>($"/api/material-flows/{flow.Data!.Id}/approve",
            new MaterialApprovalRequest { Opinion = "同意" });

        var response = await _client.PostAsJsonAsync(
            $"/api/material-flows/{flow.Data.Id}/reject",
            new MaterialRejectRequest { Reason = "想翻盘" });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(4013, "已通过的流转单不应允许驳回");
    }

    // ===== 辅助方法 =====
    private async Task SetApprovalSwitch(bool enabled)
    {
        var res = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "material.transfer.approval.enabled",
                Value = enabled ? "true" : "false",
                Description = "是否启用测试料件转移审批(false=直接转移)"
            }
        });
        res.EnsureSuccessStatusCode();
    }

    private async Task<TestProjectDto> CreateProject(string name, int? ownerId = null)
        => (await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            FollowUpIntervalDays = 14,
            Name = name,
            OwnerId = ownerId ?? 1,
            PlannedFinishDate = new DateTime(2026, 7, 29),
            ProgressCode = "testing",
            ProjectTypeCode = "prototype",
            StartDate = new DateTime(2026, 6, 29)
        })).Data!;

    private static string NewProjectCode() => $"TP-{Guid.NewGuid():N}"[..20];

    private async Task<TestMaterialDto> CreateMaterial(int projectId, string name)
        => (await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = name, ProjectId = projectId
        })).Data!;

    private async Task<UserDto> CreateUser(string employeeNo, string name, string roleCode = "employee")
        => (await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo, Name = name, Password = "123456", RoleIds = new[] { (await Role(roleCode)).Id }
        })).Data!;

    private async Task<RoleDto> Role(string code)
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        return roles!.Data!.Items.Single(x => x.Code == code);
    }

    private async Task<int> AddDisabledMaterialWorkflow()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = new WorkflowEntity
        {
            Name = "已停用测试料件流转流程",
            BizType = "material_transfer",
            IsActive = false,
            BpmnXml = null
        };
        db.Workflows.Add(workflow);
        await db.SaveChangesAsync();
        return workflow.Id;
    }

    private async Task DisableActiveMaterialWorkflow()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.Workflows.AsTracking().SingleAsync(x => x.BizType == "material_transfer" && x.IsActive);
        workflow.IsActive = false;
        await db.SaveChangesAsync();
    }

    private async Task RestoreActiveMaterialWorkflow()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.Workflows.AsTracking().FirstAsync(x => x.BizType == "material_transfer");
        workflow.IsActive = true;
        await db.SaveChangesAsync();
    }

    private async Task DeleteWorkflow(int workflowId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.Workflows.AsTracking().SingleOrDefaultAsync(x => x.Id == workflowId);
        if (workflow is null) return;
        db.Workflows.Remove(workflow);
        await db.SaveChangesAsync();
    }

    private async Task<int> ActiveMaterialWorkflowId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Workflows
            .Where(x => x.BizType == "material_transfer" && x.IsActive)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<int> UsedWorkflowId(int flowId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.MaterialFlows
            .Where(x => x.Id == flowId)
            .Select(x => x.WorkflowId)
            .SingleAsync();
    }

    private async Task Login()
    {
        Auth(await LoginToken("1001", "123456"));
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password
        });
        return body.Data!.Token;
    }

    private async Task<T> Post<T>(string url, object payload)
    {
        var res = await _client.PostAsJsonAsync(url, payload);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
