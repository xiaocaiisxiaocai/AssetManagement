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

        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "APV-TEST-001",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = 1,
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

        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "APV-SIGN-001",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = 2,
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

    private static async Task InvokeScanAndRemindAsync(PendingApprovalReminderWorker worker)
    {
        var method = typeof(PendingApprovalReminderWorker).GetMethod(
            "ScanAndRemindAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        await (Task)method!.Invoke(worker, Array.Empty<object>())!;
    }
}
