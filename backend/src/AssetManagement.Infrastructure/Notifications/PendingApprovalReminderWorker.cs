using AssetManagement.Application.Notifications;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Notifications;

/// <summary>
/// 每天早上 9 点扫描超过 1 天未处理的待审批流程，向审批人发送催办通知。
/// </summary>
public class PendingApprovalReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingApprovalReminderWorker> _logger;

    public PendingApprovalReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingApprovalReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilNineAm(stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await ScanAndRemindAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "待审批催办扫描异常");
            }
        }
    }

    private async Task WaitUntilNineAm(CancellationToken ct)
    {
        var now = DateTime.Now;
        var nextRun = now.Date.AddHours(9);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);
        var delay = nextRun - now;
        await Task.Delay(delay, ct).ContinueWith(_ => { });
    }

    internal async Task ScanAndRemindAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var threshold = DateTime.UtcNow.AddDays(-1);
        var todayStr = DateTime.Now.Date.ToString("yyyyMMdd");

        var requests = new List<CreateNotificationRequest>();

        // 扫描资产审批流
        await RemindApprovalFlowsAsync(db, threshold, todayStr, requests);

        // 扫描料件流转
        await RemindMaterialFlowsAsync(db, threshold, todayStr, requests);

        if (requests.Count > 0)
        {
            await notificationSvc.CreateBatchAsync(requests);
            _logger.LogInformation("发送待审批催办通知 {Count} 条", requests.Count);
        }
    }

    private static async Task RemindApprovalFlowsAsync(
        AppDbContext db, DateTime threshold, string todayStr,
        List<CreateNotificationRequest> requests)
    {
        var pendingFlows = await db.ApprovalFlows
            .Where(f => f.Status == "pending")
            .ToListAsync();

        var workflowIds = pendingFlows.Select(f => f.WorkflowId).Distinct().ToArray();
        var workflowMap = await db.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w);

        foreach (var flow in pendingFlows)
        {
            if (!HasCurrentNodeWaitedLongEnough(flow.CurrentNodeIds, flow.BpmnTokens, flow.ApplyTime, threshold))
                continue;

            if (!workflowMap.TryGetValue(flow.WorkflowId, out var wf) ||
                string.IsNullOrEmpty(wf.BpmnXml)) continue;

            var process = BpmnParser.Parse(wf.BpmnXml);
            var approverIds = await ResolveApproversForFlowAsync(db, flow, process);

            foreach (var uid in approverIds)
            {
                var key = $"pending_remind_{flow.Id}_{todayStr}_{uid}";
                requests.Add(new CreateNotificationRequest
                {
                    Type = "approval_reminder",
                    Title = $"待审批提醒：{flow.AssetName}",
                    Body = $"资产 {flow.AssetNo}（{flow.AssetName}）的{BizTypeLabel(flow.BizType)}申请已等待超过 1 天，请及时审批。",
                    FlowId = flow.Id,
                    UserId = uid,
                    IdempotencyKey = key,
                });
            }
        }
    }

    private static async Task RemindMaterialFlowsAsync(
        AppDbContext db, DateTime threshold, string todayStr,
        List<CreateNotificationRequest> requests)
    {
        var pendingFlows = await db.MaterialFlows
            .Where(f => f.Status == "pending")
            .ToListAsync();

        var workflowIds = pendingFlows.Select(f => f.WorkflowId).Distinct()
            .Where(id => id > 0).ToArray();
        var workflowMap = await db.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w);

        foreach (var flow in pendingFlows)
        {
            if (!HasCurrentNodeWaitedLongEnough(flow.CurrentNodeIds, flow.BpmnTokens, flow.ApplyTime, threshold))
                continue;

            if (!workflowMap.TryGetValue(flow.WorkflowId, out var wf) ||
                string.IsNullOrEmpty(wf.BpmnXml)) continue;

            var process = BpmnParser.Parse(wf.BpmnXml);
            var approverIds = await ResolveApproversForMaterialFlowAsync(db, flow, process);

            foreach (var uid in approverIds)
            {
                var key = $"pending_remind_mf_{flow.Id}_{todayStr}_{uid}";
                requests.Add(new CreateNotificationRequest
                {
                    Type = "material_approval_reminder",
                    Title = $"待审批提醒：{flow.MaterialName}",
                    Body = $"料件 {flow.MaterialNo}（{flow.MaterialName}）的流转申请已等待超过 1 天，请及时审批。",
                    FlowId = flow.Id,
                    UserId = uid,
                    IdempotencyKey = key,
                });
            }
        }
    }

    private static async Task<List<int>> ResolveApproversForFlowAsync(
        AppDbContext db, ApprovalFlow flow, BpmnProcess process)
    {
        var result = new List<int>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) ||
                token.Status != BpmnTokenStatus.Active) continue;

            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;

            var ids = await ResolveAssigneeAsync(db, node, flow.ApplicantId);
            foreach (var id in ids)
                if (!result.Contains(id)) result.Add(id);
        }
        return result;
    }

    private static bool HasCurrentNodeWaitedLongEnough(
        IEnumerable<string> currentNodeIds,
        IReadOnlyDictionary<string, BpmnToken> tokens,
        DateTime fallbackApplyTime,
        DateTime threshold)
    {
        foreach (var nodeId in currentNodeIds)
        {
            if (!tokens.TryGetValue(nodeId, out var token) || token.Status != BpmnTokenStatus.Active)
                continue;

            var startedAt = token.StartedAt ?? fallbackApplyTime;
            if (startedAt < threshold)
                return true;
        }

        return false;
    }

    private static async Task<List<int>> ResolveApproversForMaterialFlowAsync(
        AppDbContext db, MaterialFlow flow, BpmnProcess process)
    {
        var result = new List<int>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) ||
                token.Status != BpmnTokenStatus.Active) continue;

            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;

            var ids = await ResolveAssigneeAsync(db, node, flow.ApplicantId);
            foreach (var id in ids)
                if (!result.Contains(id)) result.Add(id);
        }
        return result;
    }

    private static async Task<List<int>> ResolveAssigneeAsync(AppDbContext db, BpmnNode node, int applicantId)
    {
        var result = new List<int>();
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "deptManager")
            {
                var applicant = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicantId);
                if (applicant?.DepartmentId is not null)
                {
                    var dept = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
                    if (dept?.ManagerId is not null) result.Add(dept.ManagerId.Value);
                    var deptAdmins = await db.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .Where(u => u.DepartmentId == applicant.DepartmentId &&
                                    u.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "supervisor"))
                        .Select(u => u.Id).ToListAsync();
                    foreach (var uid in deptAdmins)
                        if (!result.Contains(uid)) result.Add(uid);
                }
            }
            else if (assignee == "supervisor")
            {
                foreach (var supervisorId in await ResolveSupervisorApproverUserIdsAsync(db, applicantId))
                {
                    if (!result.Contains(supervisorId)) result.Add(supervisorId);
                }
            }
            else if (int.TryParse(assignee, out var uid)) result.Add(uid);
            else
            {
                var u = await db.Users.FirstOrDefaultAsync(x => x.Name == assignee || x.EmployeeNo == assignee);
                if (u is not null) result.Add(u.Id);
            }
        }

        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (int.TryParse(p, out var uid)) { if (!result.Contains(uid)) result.Add(uid); }
                else
                {
                    var u = await db.Users.FirstOrDefaultAsync(x => x.Name == p);
                    if (u is not null && !result.Contains(u.Id)) result.Add(u.Id);
                }
            }
        }

        if (!string.IsNullOrEmpty(candidateGroups))
        {
            var groups = candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToArray();
            var groupUsers = await db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => ur.Role != null &&
                    groups.Any(g => g == ur.Role.Code || g == ur.Role.Name)))
                .Select(u => u.Id).ToListAsync();
            foreach (var uid in groupUsers)
                if (!result.Contains(uid)) result.Add(uid);
        }

        return result;
    }

    private static async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(AppDbContext db, int applicantId)
    {
        var result = new List<int>();
        var applicant = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicantId);
        if (applicant?.DepartmentId is not null)
        {
            var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
            if (department?.ManagerId is not null)
            {
                result.Add(department.ManagerId.Value);
            }
        }

        // 与正式审批权限解析保持一致：组织负责人优先，旧库未配置负责人时再兼容直属上级字段。
        if (result.Count == 0 && applicant?.SupervisorId is not null)
        {
            result.Add(applicant.SupervisorId.Value);
        }

        return result;
    }

    private static string BizTypeLabel(string bizType) => bizType switch
    {
        "borrow" => "资产借用",
        "transfer" => "资产转让",
        "return" => "资产归还",
        _ => bizType
    };
}
