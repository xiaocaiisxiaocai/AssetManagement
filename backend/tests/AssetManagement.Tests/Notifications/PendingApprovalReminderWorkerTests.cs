using System.Reflection;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetManagement.Tests.Notifications;

public class PendingApprovalReminderWorkerTests : MySqlFixtureBase
{
    [Fact]
    public async Task ScanAndRemindAsync_supervisor_node_uses_department_manager_when_user_supervisor_missing()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString)));
        services.AddScoped<AssetManagement.Application.Notifications.INotificationService, NotificationService>();
        var provider = services.BuildServiceProvider();
        var worker = new PendingApprovalReminderWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PendingApprovalReminderWorker>.Instance);

        var manager = new User { EmployeeNo = "M001", Name = "部门负责人", PasswordHash = "x" };
        var applicant = new User { EmployeeNo = "U001", Name = "申请人", PasswordHash = "x" };
        _db.Users.AddRange(manager, applicant);
        await _db.SaveChangesAsync();

        var department = new Department { Name = "测试部", Code = "TEST", ManagerId = manager.Id };
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        applicant.DepartmentId = department.Id;
        await _db.SaveChangesAsync();

        var workflow = new Domain.Entities.Workflow
        {
            Name = "借用审批",
            BizType = "borrow",
            BpmnXml = SupervisorBpmn(),
            IsActive = true
        };
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();
        var asset = await CreateAssetAsync("A001", "测试资产");

        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "APV-TEST-001",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = "A001",
            AssetName = "测试资产",
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            Status = "pending",
            CurrentNodeIds = new List<string> { "Task_Supervisor" },
            BpmnTokens = new Dictionary<string, BpmnToken>
            {
                ["Task_Supervisor"] = new()
                {
                    NodeId = "Task_Supervisor",
                    NodeName = "直属主管审批",
                    Status = BpmnTokenStatus.Active,
                    StartedAt = DateTime.UtcNow.AddDays(-2)
                }
            },
            ApplyTime = DateTime.UtcNow.AddDays(-2),
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        await _db.SaveChangesAsync();

        await InvokeScanAndRemindAsync(worker);

        var notification = await _db.Notifications.SingleOrDefaultAsync();
        notification.Should().NotBeNull();
        notification!.Type.Should().Be("approval_reminder");
        notification.UserId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task ScanAndRemindAsync_sign_state_only_reminds_active_unsigned_users()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString)));
        services.AddScoped<AssetManagement.Application.Notifications.INotificationService, NotificationService>();
        var provider = services.BuildServiceProvider();
        var worker = new PendingApprovalReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingApprovalReminderWorker>.Instance);

        var signed = new User { EmployeeNo = "S001", Name = "已签人员", PasswordHash = "x" };
        var unsigned = new User { EmployeeNo = "S002", Name = "待签人员", PasswordHash = "x" };
        _db.Users.AddRange(signed, unsigned);
        var workflow = new Domain.Entities.Workflow
        {
            Name = "加签催办测试",
            BizType = "borrow",
            BpmnXml = SupervisorBpmn(),
            IsActive = true
        };
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();
        var asset = await CreateAssetAsync("A002", "加签测试资产");

        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "APV-SIGN-001",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = "A002",
            AssetName = "加签测试资产",
            ApplicantId = signed.Id,
            Applicant = signed.Name,
            Status = "pending",
            CurrentNodeIds = new List<string> { "Task_Supervisor" },
            BpmnTokens = new Dictionary<string, BpmnToken>
            {
                ["Task_Supervisor"] = new()
                {
                    NodeId = "Task_Supervisor",
                    NodeName = "加签审批",
                    Status = BpmnTokenStatus.Active,
                    StartedAt = DateTime.UtcNow.AddDays(-2),
                    SignStates = new Dictionary<string, bool>
                    {
                        [signed.Id.ToString()] = true,
                        [unsigned.Id.ToString()] = false
                    }
                }
            },
            ApplyTime = DateTime.UtcNow.AddDays(-2),
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        await _db.SaveChangesAsync();

        await InvokeScanAndRemindAsync(worker);

        var notifications = await _db.Notifications.ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].UserId.Should().Be(unsigned.Id);
    }

    [Fact]
    public async Task ScanAndRemindAsync_parallel_flow_only_reminds_nodes_that_reached_threshold()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString)));
        services.AddScoped<AssetManagement.Application.Notifications.INotificationService, NotificationService>();
        var provider = services.BuildServiceProvider();
        var worker = new PendingApprovalReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingApprovalReminderWorker>.Instance);

        var overdueApprover = new User { EmployeeNo = "OLD001", Name = "超时审批人", PasswordHash = "x" };
        var freshApprover = new User { EmployeeNo = "NEW001", Name = "新节点审批人", PasswordHash = "x" };
        _db.Users.AddRange(overdueApprover, freshApprover);
        var workflow = new Domain.Entities.Workflow
        {
            Name = "并行节点独立催办",
            BizType = "borrow",
            BpmnXml = ParallelBpmn(),
            IsActive = true
        };
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();
        var asset = await CreateAssetAsync("A003", "并行催办资产");

        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "APV-PARALLEL-REMIND",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = "A003",
            AssetName = "并行催办资产",
            ApplicantId = overdueApprover.Id,
            Applicant = "申请人",
            Status = "pending",
            CurrentNodeIds = ["Task_Old", "Task_Fresh"],
            BpmnTokens = new Dictionary<string, BpmnToken>
            {
                ["Task_Old"] = new()
                {
                    NodeId = "Task_Old", Status = BpmnTokenStatus.Active,
                    StartedAt = DateTime.UtcNow.AddDays(-2),
                    SignStates = new Dictionary<string, bool> { [overdueApprover.Id.ToString()] = false }
                },
                ["Task_Fresh"] = new()
                {
                    NodeId = "Task_Fresh", Status = BpmnTokenStatus.Active,
                    StartedAt = DateTime.UtcNow.AddHours(-2),
                    SignStates = new Dictionary<string, bool> { [freshApprover.Id.ToString()] = false }
                }
            },
            ApplyTime = DateTime.UtcNow.AddDays(-2),
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        await _db.SaveChangesAsync();

        await InvokeScanAndRemindAsync(worker);

        var notifications = await _db.Notifications.ToListAsync();
        notifications.Should().ContainSingle().Which.UserId.Should().Be(overdueApprover.Id);
    }

    [Fact]
    public async Task ScanAndRemindAsync_org_manager_node_uses_configured_organization_level_manager()
    {
        var worker = CreateWorker();
        var manager = new User { EmployeeNo = "ORG-MGR", Name = "事业部负责人", PasswordHash = "x" };
        var applicant = new User { EmployeeNo = "ORG-USER", Name = "组织申请人", PasswordHash = "x" };
        _db.Users.AddRange(manager, applicant);
        var level = new OrganizationLevel { Code = "department", Name = "事业部", Sort = 1, IsActive = true };
        _db.OrganizationLevels.Add(level);
        await _db.SaveChangesAsync();
        var department = new Department
        {
            Name = "组织层级事业部", Code = "ORG-DEPT", ManagerId = manager.Id,
            OrganizationLevelId = level.Id, IsActive = true
        };
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();
        applicant.DepartmentId = department.Id;
        await _db.SaveChangesAsync();

        var workflow = new Domain.Entities.Workflow
        {
            Name = "组织层级催办", BizType = "borrow",
            BpmnXml = SingleTaskBpmn("Task_Org", "orgManager:department"), IsActive = true
        };
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();
        var orgFlow = PendingFlow(workflow.Id, applicant, "Task_Org", "APV-ORG-REMIND");
        orgFlow.AssetId = (await CreateAssetAsync(orgFlow.AssetNo, orgFlow.AssetName)).Id;
        _db.ApprovalFlows.Add(orgFlow);
        await _db.SaveChangesAsync();

        await InvokeScanAndRemindAsync(worker);

        (await _db.Notifications.SingleAsync()).UserId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task ScanAndRemindAsync_transfer_receiver_task_uses_transferee_department_manager()
    {
        var worker = CreateWorker();
        var applicantManager = new User { EmployeeNo = "APP-MGR", Name = "申请方主管", PasswordHash = "x" };
        var receiverManager = new User { EmployeeNo = "REC-MGR", Name = "接收方主管", PasswordHash = "x" };
        var applicant = new User { EmployeeNo = "APP-USER", Name = "转让申请人", PasswordHash = "x" };
        var transferee = new User { EmployeeNo = "REC-USER", Name = "接收人", PasswordHash = "x" };
        _db.Users.AddRange(applicantManager, receiverManager, applicant, transferee);
        await _db.SaveChangesAsync();
        var applicantDept = new Department { Name = "申请部门", Code = "APP-DEPT", ManagerId = applicantManager.Id };
        var receiverDept = new Department { Name = "接收部门", Code = "REC-DEPT", ManagerId = receiverManager.Id };
        _db.Departments.AddRange(applicantDept, receiverDept);
        await _db.SaveChangesAsync();
        applicant.DepartmentId = applicantDept.Id;
        transferee.DepartmentId = receiverDept.Id;
        await _db.SaveChangesAsync();

        var workflow = new Domain.Entities.Workflow
        {
            Name = "接收方主管催办", BizType = "transfer",
            BpmnXml = SingleTaskBpmn("Task_receiver", "deptManager"), IsActive = true
        };
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();
        var flow = PendingFlow(workflow.Id, applicant, "Task_receiver", "APV-RECEIVER-REMIND");
        flow.AssetId = (await CreateAssetAsync(flow.AssetNo, flow.AssetName)).Id;
        flow.BizType = "transfer";
        flow.TransfereeId = transferee.Id;
        flow.Transferee = transferee.Name;
        _db.ApprovalFlows.Add(flow);
        await _db.SaveChangesAsync();

        await InvokeScanAndRemindAsync(worker);

        var notification = await _db.Notifications.SingleAsync();
        notification.UserId.Should().Be(receiverManager.Id);
        notification.UserId.Should().NotBe(applicantManager.Id);
    }

    [Fact]
    public async Task ScanAndRemindAsync_honors_pre_cancelled_token()
    {
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = () => worker.ScanAndRemindAsync(cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static string SupervisorBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  targetNamespace="http://asset-management/workflow">
  <bpmn:process id="Process_Supervisor" isExecutable="true">
    <bpmn:startEvent id="Start" name="开始" />
    <bpmn:userTask id="Task_Supervisor" name="直属主管审批" camunda:assignee="supervisor" />
    <bpmn:endEvent id="End" name="结束" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Task_Supervisor" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Task_Supervisor" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string ParallelBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="Process_ParallelReminder" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:parallelGateway id="Fork" />
    <bpmn:userTask id="Task_Old" />
    <bpmn:userTask id="Task_Fresh" />
    <bpmn:sequenceFlow id="F1" sourceRef="Start" targetRef="Fork" />
    <bpmn:sequenceFlow id="F2" sourceRef="Fork" targetRef="Task_Old" />
    <bpmn:sequenceFlow id="F3" sourceRef="Fork" targetRef="Task_Fresh" />
  </bpmn:process>
</bpmn:definitions>
""";

    private PendingApprovalReminderWorker CreateWorker()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString)));
        services.AddScoped<AssetManagement.Application.Notifications.INotificationService, NotificationService>();
        var provider = services.BuildServiceProvider();
        return new PendingApprovalReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingApprovalReminderWorker>.Instance);
    }

    private static ApprovalFlow PendingFlow(
        int workflowId,
        User applicant,
        string nodeId,
        string flowNo) => new()
    {
        FlowNo = flowNo,
        BizType = "borrow",
        WorkflowId = workflowId,
        AssetId = Math.Abs(flowNo.GetHashCode()),
        AssetNo = flowNo,
        AssetName = "催办测试资产",
        ApplicantId = applicant.Id,
        Applicant = applicant.Name,
        Status = "pending",
        CurrentNodeIds = [nodeId],
        BpmnTokens = new Dictionary<string, BpmnToken>
        {
            [nodeId] = new()
            {
                NodeId = nodeId, Status = BpmnTokenStatus.Active,
                StartedAt = DateTime.UtcNow.AddDays(-2)
            }
        },
        ApplyTime = DateTime.UtcNow.AddDays(-2),
        Deadline = DateTime.UtcNow.AddDays(1)
    };

    private async Task<Asset> CreateAssetAsync(string assetNo, string name)
    {
        var category = new AssetCategory
        {
            CodeSeg = Guid.NewGuid().ToString("N")[..8],
            Code = Guid.NewGuid().ToString("N"),
        };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = assetNo,
            Name = name,
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private static string SingleTaskBpmn(string nodeId, string assignee) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Reminder" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:userTask id="{{nodeId}}" camunda:assignee="{{assignee}}" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="F1" sourceRef="Start" targetRef="{{nodeId}}" />
    <bpmn:sequenceFlow id="F2" sourceRef="{{nodeId}}" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static async Task InvokeScanAndRemindAsync(PendingApprovalReminderWorker worker)
        => await worker.ScanAndRemindAsync();
}
