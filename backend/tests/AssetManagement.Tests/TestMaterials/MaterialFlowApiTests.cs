using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Tests.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        flow.Data.Status.Should().Be("receiving");
        flow.Data.CanWithdraw.Should().BeTrue();
        flow.Data.ProjectId.Should().Be(project.Id);
        (await UsedWorkflowId(flow.Data.Id)).Should().BeNull("直接转移不应使用 0 伪造流程外键");

        var before = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        before!.Data!.CustodianId.Should().NotBe(transferee.Id);

        Auth(await LoginToken(transferee.EmployeeNo, "TestPass123"));
        var receiving = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/receiving-page?projectId={project.Id}&flowId={flow.Data.Id}");
        receiving!.Data!.Items.Should().ContainSingle(x => x.Id == flow.Data.Id && x.ProjectId == project.Id);

        var confirmed = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/confirm-receipt", new { });
        confirmed.Data!.Status.Should().Be("approved");
        confirmed.Data.CanWithdraw.Should().BeFalse();

        await Login();
        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(transferee.Id);
        after.Data.HasPendingFlow.Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = await db.Notifications.AsNoTracking()
            .Where(x => x.FlowId == flow.Data.Id)
            .Select(x => x.Type)
            .ToListAsync();
        notifications.Should().Contain("material_receipt_pending").And.Contain("material_received");
        notifications.Should().NotContain("material_transferred").And.NotContain("material_approved");
    }

    [Fact]
    public async Task Landing_project_rejects_new_material_transfer()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("落地后禁止流转项目");
        var transferee = await CreateUser("landing-transfer", "落地后接收人");
        var material = await CreateMaterial(project.Id, "落地后禁止流转料件");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == project.Id);
            entity.ProgressCode = "landing";
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "落地后不应流转"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.Code.Should().Be(4092);
    }

    [Fact]
    public async Task Direct_transfer_and_disabling_transferee_cannot_commit_an_inactive_custodian()
    {
        await Login();
        await SetApprovalSwitch(false);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var project = await CreateProject($"并发停用项目-{attempt}");
            var transferee = await CreateUser($"race-user-{attempt}", $"并发受让人-{attempt}");
            var material = await CreateMaterial(project.Id, $"并发停用样品-{attempt}");

            var transferTask = _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
            {
                MaterialId = material.Id, TransfereeId = transferee.Id, Reason = "与停用并发"
            });
            var disableTask = _client.PostAsJsonAsync($"/api/users/{transferee.Id}/toggle-status", new { isActive = false });
            await Task.WhenAll(transferTask, disableTask);
            var transfer = await transferTask.Result.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();
            var disable = await disableTask.Result.Content.ReadFromJsonAsync<ApiResult<object?>>();
            var currentMaterial = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
            var users = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>($"/api/users?keyword={transferee.EmployeeNo}");
            var currentUser = users!.Data!.Items.Single(user => user.Id == transferee.Id);
            var current = currentMaterial!.Data!;
            var disableResult = disable!;

            if (transfer!.Code == 0)
                transfer.Data!.Status.Should().Be("receiving");
            current.CustodianId.Should().NotBe(transferee.Id,
                $"attempt={attempt}, transfer={transfer!.Code}/{transfer.Message}, " +
                $"disable={disableResult.Code}/{disableResult.Message}, userActive={currentUser.IsActive}");
        }
    }

    [Fact]
    public async Task Transfer_rejects_current_custodian_as_transferee()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("禁止原地转移项目");
        var custodian = await CreateUser("0999", "当前保管人");
        var material = await CreateMaterial(project.Id, "禁止原地转移样品", custodian.DepartmentId, custodian.Id);

        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = custodian.Id,
            Reason = "不应产生无意义流转"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(4001);
        body.Message.Should().Be("接收人不能是当前保管人");
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

        detail!.Data!.Material.CustodianId.Should().NotBe(transferee.Id);
        detail.Data.Flows.Should().ContainSingle(x =>
            x.Id == flow.Data!.Id &&
            x.Status == "receiving" &&
            x.ProjectId == project.Id &&
            x.Transferee == transferee.Name &&
            x.Reason == "详情应保留流转历史");
        detail.Data.Records.Should().ContainSingle(x =>
            x.Action == "direct_transfer" &&
            x.Operator == "系统管理员" &&
            x.Comment!.Contains($"待 {transferee.Name} 确认接收"));
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

        // 默认项目负责人分支明确将审批节点分配给工号 1001。
        var approved = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/approve", new MaterialApprovalRequest { Opinion = "同意" });
        approved.Data!.Status.Should().Be("receiving");
        approved.Data.CanWithdraw.Should().BeTrue();

        var handled = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/handled-page?page=1&pageSize=20&flowId={flow.Data.Id}");
        var handledRecord = handled!.Data!.Items.Should().ContainSingle().Which;
        handledRecord.MyApprovalAction.Should().Be("approve");
        handledRecord.MyApprovalNodeId.Should().NotBeNullOrWhiteSpace();
        handledRecord.MyApprovalTime.Should().NotBeNull();

        var waiting = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        waiting!.Data!.CustodianId.Should().NotBe(transferee.Id);
        waiting.Data.HasPendingFlow.Should().BeTrue();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notes = await db.Notifications.AsNoTracking()
                .Where(x => x.FlowId == flow.Data.Id)
                .ToListAsync();
            notes.Should().Contain(x => x.Type == "material_approved" && x.Body.Contains("待接收人确认"));
            notes.Should().Contain(x => x.Type == "material_receipt_pending");
            notes.Should().NotContain(x => x.Type == "material_transferred");
        }

        Auth(await LoginToken(transferee.EmployeeNo, "TestPass123"));
        var confirmed = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/confirm-receipt", new { });
        confirmed.Data!.Status.Should().Be("approved");

        await Login();
        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(transferee.Id);
        after.Data.HasPendingFlow.Should().BeFalse();
    }

    [Fact]
    public async Task Material_approval_and_custodian_change_roll_back_when_notification_write_fails()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("料件通知事务项目");
        var transferee = await CreateUser("atomic-user", "事务接收人");
        var material = await CreateMaterial(project.Id, "通知事务料件");
        var originalCustodianId = material.CustodianId;
        var started = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "验证通知与料件流转共享事务"
        });
        var flowId = started.Data!.Id;
        var approverToken = await LoginToken("1001", "123456");

        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INotificationService>();
                services.AddScoped<INotificationService, FailingNotificationService>();
            }));
        using var failingClient = failingFactory.CreateClient();
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approverToken);

        var response = await failingClient.PostAsJsonAsync($"/api/material-flows/{flowId}/approve",
            new MaterialApprovalRequest { Opinion = "同意，但通知写入失败" });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(500);
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedFlow = await db.MaterialFlows.AsNoTracking().SingleAsync(item => item.Id == flowId);
        savedFlow.Status.Should().Be("pending", "通知写入失败时料件审批状态不能单独提交");
        var savedMaterial = await db.TestMaterials.AsNoTracking().SingleAsync(item => item.Id == material.Id);
        savedMaterial.CustodianId.Should().Be(originalCustodianId, "保管人变更必须与通知原子提交");
        (await db.MaterialFlowRecords.AsNoTracking().AnyAsync(record =>
            record.FlowId == flowId && record.Action == "approve")).Should().BeFalse();
        (await db.Notifications.AsNoTracking().AnyAsync(notification =>
            notification.FlowId == flowId && notification.Type == "material_approved")).Should().BeFalse();
        (await db.Notifications.AsNoTracking().AnyAsync(notification =>
            notification.FlowId == flowId && notification.Type == "material_receipt_pending")).Should().BeFalse();
    }

    [Fact]
    public async Task Material_flow_page_endpoints_filter_before_count_and_support_flow_id()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("料件分页项目");
        var transferee = await CreateUser("page-user", "分页受让人");
        var material = await CreateMaterial(project.Id, "分页目标样品");
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id, TransfereeId = transferee.Id, Reason = "分页目标"
        });
        var secondTransferee = await CreateUser("page-user-2", "分页受让人二");
        var secondMaterial = await CreateMaterial(project.Id, "分页目标样品二");
        await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = secondMaterial.Id, TransfereeId = secondTransferee.Id, Reason = "分页目标二"
        });

        var pending = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/pending-page?page=1&pageSize=1&flowId={flow.Data!.Id}&keyword={material.Name}&status=pending&projectId={project.Id}");
        var mine = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/mine-page?page=1&pageSize=1&flowId={flow.Data.Id}&keyword={flow.Data.FlowNo}&status=pending&projectId={project.Id}");

        pending!.Data!.Total.Should().Be(1);
        pending.Data.Items.Should().ContainSingle().Which.Id.Should().Be(flow.Data.Id);
        mine!.Data!.Total.Should().Be(1);
        mine.Data.Items.Should().ContainSingle().Which.Id.Should().Be(flow.Data.Id);

        _factory.CommandCounter.Reset();
        var onePending = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/pending-page?page=1&pageSize=1&flowId={flow.Data.Id}&projectId={project.Id}");
        var onePendingQueries = _factory.CommandCounter.ReaderCount;
        _factory.CommandCounter.Reset();
        var twoPending = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/pending-page?page=1&pageSize=2&keyword=分页目标样品&projectId={project.Id}");
        var twoPendingQueries = _factory.CommandCounter.ReaderCount;
        onePending!.Data!.Total.Should().Be(1);
        twoPending!.Data!.Total.Should().Be(2);
        twoPendingQueries.Should().BeLessThanOrEqualTo(onePendingQueries + 1,
            "料件待办扫描应跨同模板同上下文流程复用审批人解析");

        var extremePending = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/pending-page?page={int.MaxValue}&pageSize=100&projectId={project.Id}");
        extremePending!.Code.Should().Be(0);
        extremePending.Data!.Items.Should().BeEmpty();
        var extremeMine = await _client.GetFromJsonAsync<ApiResult<PagedResult<MaterialFlowDto>>>(
            $"/api/material-flows/mine-page?page={int.MaxValue}&pageSize=100&projectId={project.Id}");
        extremeMine!.Code.Should().Be(0);
        extremeMine.Data!.Items.Should().BeEmpty();

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMaterialFlowService>();
        var transfereeMine = await service.MinePageAsync(transferee.Id, new MaterialFlowPageQuery
        {
            FlowId = flow.Data.Id,
            Page = 1,
            PageSize = 10
        });
        transfereeMine.Total.Should().Be(0, "mine-page 表示我的发起，受让人仅能通过详情查看与自己相关的流转");
    }

    [Fact]
    public async Task Admin_cannot_handle_material_node_assigned_to_supervisor()
    {
        await Login();
        await SetApprovalSwitch(true);
        var originalBpmn = await ReplaceActiveMaterialWorkflowBpmn(SupervisorOnlyBpmn());
        try
        {
            var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments",
                new CreateDepartmentRequest { Name = "料件审批隔离部门" });
            var supervisor = await CreateUser("0941", "料件审批主管", "supervisor", department.Data!.Id);
            var transferee = await CreateUser("0942", "料件流转接收人");
            var project = await CreateProject("料件审批隔离项目");
            var material = await CreateMaterial(
                project.Id,
                "料件审批隔离样品",
                department.Data.Id,
                supervisor.Id);

            Auth(await LoginToken(supervisor.EmployeeNo, "TestPass123"));
            var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
            {
                MaterialId = material.Id,
                TransfereeId = transferee.Id,
                Reason = "验证管理员不能越级处理料件流转"
            });

            await Login();
            var pending = await _client.GetFromJsonAsync<ApiResult<List<MaterialFlowDto>>>("/api/material-flows/pending");
            pending!.Data.Should().NotContain(x => x.Id == flow.Data!.Id);

            var rejected = await _client.PostAsJsonAsync($"/api/material-flows/{flow.Data!.Id}/reject",
                new MaterialRejectRequest { Reason = "管理员越级驳回" });
            var rejectedBody = await rejected.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();
            rejectedBody!.Code.Should().Be(4016);

            var approved = await _client.PostAsJsonAsync($"/api/material-flows/{flow.Data.Id}/approve",
                new MaterialApprovalRequest { Opinion = "管理员越级通过" });
            var approvedBody = await approved.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();
            approvedBody!.Code.Should().Be(4016);

            Auth(await LoginToken(supervisor.EmployeeNo, "TestPass123"));
            var supervisorPending = await _client.GetFromJsonAsync<ApiResult<List<MaterialFlowDto>>>(
                "/api/material-flows/pending");
            supervisorPending!.Data.Should().Contain(x => x.Id == flow.Data.Id);
            var supervisorApproved = await Post<ApiResult<MaterialFlowDto>>(
                $"/api/material-flows/{flow.Data.Id}/approve",
                new MaterialApprovalRequest { Opinion = "主管正常通过" });
            supervisorApproved.Data!.Status.Should().Be("receiving");
        }
        finally
        {
            await ReplaceActiveMaterialWorkflowBpmn(originalBpmn);
        }
    }

    [Fact]
    public async Task Applicant_can_withdraw_pending_material_flow_and_start_again()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("撤回流转项目");
        var transferee = await CreateUser("0998", "撤回流转接收人");
        var material = await CreateMaterial(project.Id, "撤回流转样品");
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "稍后撤回"
        });

        var withdrawn = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data!.Id}/withdraw", new { });
        withdrawn.Data!.Status.Should().Be("withdrawn");
        withdrawn.Data.CurrentNodeIds.Should().BeEmpty();

        var detail = await _client.GetFromJsonAsync<ApiResult<TestMaterialDetailDto>>(
            $"/api/test-materials/{material.Id}/detail");
        detail!.Data!.Records.Should().Contain(x =>
            x.Action == "withdraw" && x.Comment == "申请人主动撤回");
        detail.Data.Material.HasPendingFlow.Should().BeFalse();

        var replacement = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "撤回后重新发起"
        });
        replacement.Code.Should().Be(0, "撤回后应释放料件的进行中流转锁");
    }

    [Fact]
    public async Task Definition_missing_material_flow_can_be_released_without_changing_custodian()
    {
        await Login();
        await SetApprovalSwitch(true);
        var project = await CreateProject("缺失节点流转项目");
        var transferee = await CreateUser($"mf{Guid.NewGuid():N}"[..8], "缺失节点接收人");
        var healthyMaterial = await CreateMaterial(project.Id, "仍可审批样品");
        var healthy = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = healthyMaterial.Id,
            TransfereeId = transferee.Id,
            Reason = "正常流转不能终止"
        });
        var rejected = await _client.PostAsJsonAsync(
            $"/api/material-flows/{healthy.Data!.Id}/release-stuck", new { });
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var material = await CreateMaterial(project.Id, "缺失节点样品");
        var before = await _client.GetFromJsonAsync<ApiResult<TestMaterialDetailDto>>(
            $"/api/test-materials/{material.Id}/detail");
        int flowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await db.Users.AsNoTracking()
                .Where(user => user.EmployeeNo == "1001")
                .Select(user => user.Id)
                .SingleAsync();
            var workflowId = await db.Workflows.AsNoTracking()
                .Where(workflow => workflow.BizType == "material_transfer" && workflow.IsActive)
                .Select(workflow => workflow.Id)
                .SingleAsync();
            var flow = new MaterialFlow
            {
                FlowNo = $"MF-{Guid.NewGuid():N}"[..20],
                BizType = "material_transfer",
                WorkflowId = workflowId,
                MaterialId = material.Id,
                MaterialNo = material.MaterialNo,
                MaterialName = material.Name,
                ApplicantId = adminId,
                Applicant = "系统管理员",
                TransfereeId = transferee.Id,
                Transferee = transferee.Name,
                Status = FlowStatus.Pending,
                ActiveScopeKey = $"material:{material.Id}",
                CurrentNodeIds = new List<string> { "Task_missingNode" },
                BpmnTokens = new Dictionary<string, BpmnToken>
                {
                    ["Task_missingNode"] = new()
                    {
                        NodeId = "Task_missingNode",
                        NodeName = "已删除节点",
                        Status = BpmnTokenStatus.Active,
                        StartedAt = DateTime.UtcNow
                    }
                },
                ApplyTime = DateTime.UtcNow,
                Deadline = DateTime.UtcNow.AddDays(1)
            };
            db.MaterialFlows.Add(flow);
            await db.SaveChangesAsync();
            flowId = flow.Id;
        }

        var released = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flowId}/release-stuck", new { });
        released.Data!.Status.Should().Be(FlowStatus.Withdrawn);
        released.Data.CanReleaseStuck.Should().BeFalse();

        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDetailDto>>(
            $"/api/test-materials/{material.Id}/detail");
        after!.Data!.Material.CustodianId.Should().Be(before!.Data!.Material.CustodianId);
        after.Data.Records.Should().Contain(x => x.Action == "release");

        var replacement = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "终止后重新发起"
        });
        replacement.Code.Should().Be(0);
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
        var owner = await CreateUser("0910", "项目负责人甲", "employee");
        var transferee = await CreateUser("0911", "受让人甲");
        var project = await CreateProject("负责人流转项目", owner.Id);
        var material = await CreateMaterial(project.Id, "负责人审批样品");

        Auth(await LoginToken(owner.EmployeeNo, "TestPass123"));
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
        denied.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden,
            "普通员工即使是项目负责人也不具备审批权限");

        await Login();
        var pending = await _client.GetFromJsonAsync<ApiResult<List<MaterialFlowDto>>>("/api/material-flows/pending");
        pending!.Data!.Select(x => x.Id).Should().Contain(flow.Data.Id);

        var approved = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/approve",
            new MaterialApprovalRequest { Opinion = "指定人员审批通过" });
        approved.Data!.Status.Should().Be("receiving");
    }

    [Fact]
    public async Task Employee_cannot_transfer_unrelated_material()
    {
        await Login();
        var employee = await CreateUser("0931", "无关员工");
        var transferee = await CreateUser("0932", "无关流转接收人");
        var project = await CreateProject("无关流转项目");
        var material = await CreateMaterial(project.Id, "非本人料件");

        Auth(await LoginToken(employee.EmployeeNo, "TestPass123"));
        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "无关员工不应发起流转"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(4047);
    }

    [Fact]
    public async Task Admin_role_alone_cannot_transfer_unrelated_material()
    {
        await Login();
        await SetApprovalSwitch(false);
        var owner = await CreateUser("0943", "料件实际负责人");
        var transferee = await CreateUser("0944", "管理员转移接收人");
        var project = await CreateProject("管理员不可越权转移项目", owner.Id);
        var material = await CreateMaterial(project.Id, "管理员不可越权转移样品", owner.DepartmentId, owner.Id);

        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "管理员角色本身不应获得料件转移权"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(4047);
        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task Project_owner_transfer_notifies_configured_employee_no_assignee()
    {
        await Login();
        await SetApprovalSwitch(true);
        var owner = await CreateUser("0921", "项目负责人通知", "employee");
        var transferee = await CreateUser("0922", "通知接收人");
        var project = await CreateProject("负责人通知项目", owner.Id);
        var material = await CreateMaterial(project.Id, "负责人通知样品");

        Auth(await LoginToken(owner.EmployeeNo, "TestPass123"));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transferee_who_cannot_see_project_cannot_be_initiated(bool approvalEnabled)
    {
        await Login();
        await SetApprovalSwitch(approvalEnabled);
        var outside = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = $"看不见项目{Guid.NewGuid():N}"[..24]
        });
        var transferee = await CreateUser(
            $"nv{Guid.NewGuid():N}"[..12],
            "看不见项目的接收人",
            departmentId: outside.Data!.Id);
        var project = await CreateProject(
            $"接收人不可见项目-{(approvalEnabled ? "on" : "off")}-{Guid.NewGuid():N}");
        var material = await CreateMaterial(
            project.Id,
            "接收人不可见样品",
            await UserDepartmentId(1),
            1);

        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "接收人看不见项目"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body!.Code.Should().Be(4031);
        body.Message.Should().Be("接收人无权查看该项目");
        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(1);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.MaterialFlows.CountAsync(x => x.MaterialId == material.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Project_owner_without_department_fails_department_check()
    {
        await Login();
        await SetApprovalSwitch(false);
        var owner = await CreateUser($"ow{Guid.NewGuid():N}"[..12], "无部门负责人");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsTracking().SingleAsync(x => x.Id == owner.Id);
            user.DepartmentId = null;
            await db.SaveChangesAsync();
        }

        var project = await CreateProject("无部门负责人项目", owner.Id);
        var material = await CreateMaterial(
            project.Id,
            "无部门负责人样品",
            await UserDepartmentId(1),
            1);
        var response = await _client.PostAsJsonAsync("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = owner.Id,
            Reason = "负责人没有部门"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body!.Code.Should().Be(4001);
        body.Message.Should().Be("接收人必须属于有效部门");
    }

    [Fact]
    public async Task Transferee_options_returns_visible_users_only()
    {
        await Login();
        var adminDepartmentId = await AdminDepartmentId();
        var token = $"TQ{Guid.NewGuid():N}"[..8];
        var outside = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = $"候选隔离{Guid.NewGuid():N}"[..24]
        });
        var custodian = await CreateUser($"{token}00", "当前保管候选人", departmentId: adminDepartmentId);
        var named = await CreateUser($"{token}01", $"候选姓名{token}", departmentId: adminDepartmentId);
        for (var index = 2; index <= 22; index++)
        {
            await CreateUser($"{token}{index:00}", $"候选接收人{index:00}", departmentId: adminDepartmentId);
        }

        var hidden = await CreateUser($"{token}98", "看不见项目的候选人", departmentId: outside.Data!.Id);
        var project = await CreateProject("接收人候选项目");
        var material = await CreateMaterial(
            project.Id,
            "接收人候选样品",
            custodian.DepartmentId,
            custodian.Id);

        var listed = await _client.GetFromJsonAsync<ApiResult<List<TransfereeOptionDto>>>(
            $"/api/material-flows/transferee-options?materialId={material.Id}&keyword={token}");
        var byName = await _client.GetFromJsonAsync<ApiResult<List<TransfereeOptionDto>>>(
            $"/api/material-flows/transferee-options?materialId={material.Id}&keyword={Uri.EscapeDataString(named.Name)}");
        var self = await _client.GetFromJsonAsync<ApiResult<List<TransfereeOptionDto>>>(
            $"/api/material-flows/transferee-options?materialId={material.Id}&keyword=1001");

        listed!.Data.Should().HaveCount(20);
        listed.Data.Select(x => x.EmployeeNo).Should().Equal(
            Enumerable.Range(1, 20).Select(index => $"{token}{index:00}"));
        listed.Data.Should().OnlyContain(x =>
            x.DepartmentName == named.DepartmentName && !string.IsNullOrWhiteSpace(x.DepartmentName));
        listed.Data.Should().NotContain(x => x.Id == custodian.Id || x.Id == hidden.Id);
        listed.Data.Should().ContainSingle(x =>
            x.Id == named.Id && x.EmployeeNo == named.EmployeeNo && x.Name == named.Name);
        byName!.Data.Should().ContainSingle(x => x.Id == named.Id);
        self!.Data.Should().NotContain(x => x.EmployeeNo == "1001");
    }

    [Fact]
    public async Task Non_transferee_cannot_confirm_receipt()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("非接收人确认项目");
        var transferee = await CreateUser($"nt{Guid.NewGuid():N}"[..12], "真正的接收人");
        var material = await CreateMaterial(project.Id, "非接收人确认样品");
        var originalCustodian = material.CustodianId;
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "只允许接收人确认"
        });

        var response = await _client.PostAsJsonAsync(
            $"/api/material-flows/{flow.Data!.Id}/confirm-receipt", new { });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body!.Code.Should().Be(4031);
        body.Message.Should().Be("只有接收人本人可以确认接收");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.MaterialFlows.AsNoTracking().SingleAsync(x => x.Id == flow.Data.Id);
        saved.Status.Should().Be("receiving");
        saved.ActiveScopeKey.Should().Be($"material:{material.Id}");
        var savedMaterial = await db.TestMaterials.AsNoTracking().SingleAsync(x => x.Id == material.Id);
        savedMaterial.CustodianId.Should().Be(originalCustodian);
    }

    [Fact]
    public async Task Receiving_flow_can_be_withdrawn_without_changing_custodian()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("待接收撤回项目");
        var transferee = await CreateUser($"wd{Guid.NewGuid():N}"[..12], "待接收撤回接收人");
        var material = await CreateMaterial(project.Id, "待接收撤回样品");
        var originalCustodian = material.CustodianId;
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "发起后撤回"
        });
        flow.Data!.Status.Should().Be("receiving");
        flow.Data.CanWithdraw.Should().BeTrue();

        var withdrawn = await Post<ApiResult<MaterialFlowDto>>(
            $"/api/material-flows/{flow.Data.Id}/withdraw", new { });
        withdrawn.Data!.Status.Should().Be("withdrawn");
        withdrawn.Data.CanWithdraw.Should().BeFalse();

        var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{material.Id}");
        after!.Data!.CustodianId.Should().Be(originalCustodian);
        after.Data.HasPendingFlow.Should().BeFalse();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.MaterialFlows.AsNoTracking().SingleAsync(x => x.Id == flow.Data.Id);
            saved.ActiveScopeKey.Should().BeNull();
            (await db.MaterialFlowRecords.AsNoTracking().AnyAsync(x =>
                x.FlowId == flow.Data.Id && x.Action == "withdraw")).Should().BeTrue();
        }

        var replacement = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "撤回后再次发起"
        });
        replacement.Data!.Status.Should().Be("receiving");
    }

    [Fact]
    public async Task Receiving_flow_blocks_landing_follow_up()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("待接收阻止落地项目");
        var transferee = await CreateUser($"ld{Guid.NewGuid():N}"[..12], "阻止落地接收人");
        var material = await CreateMaterial(project.Id, "阻止落地样品");
        await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "待接收期间不能落地"
        });
        uint rowVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            rowVersion = await db.TestProjects.AsNoTracking()
                .Where(x => x.Id == project.Id)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        var response = await _client.PutAsJsonAsync($"/api/test-projects/{project.Id}/progress",
            new UpdateTestProjectProgressRequest { ProgressCode = "landing", RowVersion = rowVersion });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.Code.Should().Be(4092);
        body.Message.Should().Be("项目存在进行中的料件流转，请先处理完再进入落地跟进或结案");
        using var verify = _factory.Services.CreateScope();
        var saved = await verify.ServiceProvider.GetRequiredService<AppDbContext>().TestProjects
            .AsNoTracking().SingleAsync(x => x.Id == project.Id);
        saved.ProgressCode.Should().Be("testing");
    }

    [Fact]
    public async Task Confirm_receipt_rejects_when_transferee_can_no_longer_see_project()
    {
        await Login();
        await SetApprovalSwitch(false);
        var transferee = await CreateUser($"hv{Guid.NewGuid():N}"[..12], "后来看不见的接收人");
        var project = await CreateProject($"确认时不可见-{Guid.NewGuid():N}");
        var material = await CreateMaterial(project.Id, "确认时不可见样品", await UserDepartmentId(1), 1);
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "确认前失去可见性"
        });
        flow.Data!.Status.Should().Be("receiving");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var outside = new Department
            {
                Code = $"OS{Guid.NewGuid():N}"[..12],
                Name = $"确认后外部部门{Guid.NewGuid():N}"[..24],
                IsActive = true
            };
            db.Departments.Add(outside);
            await db.SaveChangesAsync();
            var user = await db.Users.AsTracking().SingleAsync(x => x.Id == transferee.Id);
            user.DepartmentId = outside.Id;
            await db.SaveChangesAsync();
        }

        Auth(await LoginToken(transferee.EmployeeNo, "TestPass123"));
        var response = await _client.PostAsJsonAsync(
            $"/api/material-flows/{flow.Data.Id}/confirm-receipt", new { });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.Code.Should().Be(4091);
        body.Message.Should().Be("您已不能查看该项目，无法确认接收。请联系申请人撤回后重新发起。");
        using var verify = _factory.Services.CreateScope();
        var savedDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedFlow = await savedDb.MaterialFlows.AsNoTracking().SingleAsync(x => x.Id == flow.Data.Id);
        savedFlow.Status.Should().Be("receiving");
        savedFlow.ActiveScopeKey.Should().Be($"material:{material.Id}");
        var savedMaterial = await savedDb.TestMaterials.AsNoTracking().SingleAsync(x => x.Id == material.Id);
        savedMaterial.CustodianId.Should().Be(1);
        savedMaterial.DepartmentId.Should().Be(await UserDepartmentId(1));
    }

    [Fact]
    public async Task Confirm_receipt_rolls_back_when_notification_write_fails()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("确认通知事务项目");
        var transferee = await CreateUser($"cf{Guid.NewGuid():N}"[..12], "确认事务接收人");
        var material = await CreateMaterial(project.Id, "确认通知事务料件");
        var originalCustodianId = material.CustodianId;
        var started = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Id,
            TransfereeId = transferee.Id,
            Reason = "确认通知失败应回滚"
        });
        var flowId = started.Data!.Id;
        var transfereeToken = await LoginToken(transferee.EmployeeNo, "TestPass123");

        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INotificationService>();
                services.AddScoped<INotificationService, FailingNotificationService>();
            }));
        using var failingClient = failingFactory.CreateClient();
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", transfereeToken);

        var response = await failingClient.PostAsJsonAsync(
            $"/api/material-flows/{flowId}/confirm-receipt", new { });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<MaterialFlowDto>>();

        body!.Code.Should().Be(500);
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedFlow = await db.MaterialFlows.AsNoTracking().SingleAsync(item => item.Id == flowId);
        savedFlow.Status.Should().Be("receiving");
        savedFlow.ActiveScopeKey.Should().Be($"material:{material.Id}");
        var savedMaterial = await db.TestMaterials.AsNoTracking().SingleAsync(item => item.Id == material.Id);
        savedMaterial.CustodianId.Should().Be(originalCustodianId);
        (await db.MaterialFlowRecords.AsNoTracking().AnyAsync(record =>
            record.FlowId == flowId && record.Action == "confirm_receipt")).Should().BeFalse();
        (await db.Notifications.AsNoTracking().AnyAsync(notification =>
            notification.FlowId == flowId && notification.Type == "material_received")).Should().BeFalse();
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

    private async Task<TestMaterialDto> CreateMaterial(
        int projectId,
        string name,
        int? departmentId = null,
        int? custodianId = null)
        => (await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = name,
            ProjectId = projectId,
            DepartmentId = departmentId,
            CustodianId = custodianId
        })).Data!;

    private async Task<UserDto> CreateUser(
        string employeeNo,
        string name,
        string roleCode = "employee",
        int? departmentId = null)
    {
        departmentId ??= await AdminDepartmentId();
        return (await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = name,
            Password = "TestPass123",
            DepartmentId = departmentId,
            RoleIds = new[] { (await Role(roleCode)).Id }
        })).Data!;
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

    private static string SupervisorOnlyBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_material_supervisor" isExecutable="true">
    <bpmn:startEvent id="StartEvent_1"><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:userTask id="Task_supervisor" name="主管审批" camunda:candidateGroups="supervisor">
      <bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id="EndEvent_1"><bpmn:incoming>Flow_2</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="Task_supervisor" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_supervisor" targetRef="EndEvent_1" />
  </bpmn:process>
</bpmn:definitions>
""";

    private async Task<int?> UserDepartmentId(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.DepartmentId)
            .SingleAsync();
    }

    private async Task<int> AdminDepartmentId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.Users.AsTracking().SingleAsync(x => x.EmployeeNo == "1001");
        if (admin.DepartmentId is int departmentId &&
            await db.Departments.AnyAsync(x => x.Id == departmentId && x.IsActive))
        {
            return departmentId;
        }

        var department = new Department
        {
            Code = $"AD{Guid.NewGuid():N}"[..12],
            Name = "管理员所属部门",
            IsActive = true
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        admin.DepartmentId = department.Id;
        await db.SaveChangesAsync();
        return department.Id;
    }

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

    private async Task<int?> UsedWorkflowId(int flowId)
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
