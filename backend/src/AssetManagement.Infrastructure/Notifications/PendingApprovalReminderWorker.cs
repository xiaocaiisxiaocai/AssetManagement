using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Workflow;
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
            try
            {
                await WaitUntilNineAm(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
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
        var now = BusinessClock.Now;
        var nextRun = now.Date.AddHours(9);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);
        var delay = nextRun - now;
        await Task.Delay(delay, ct);
    }

    internal async Task ScanAndRemindAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var threshold = DateTime.UtcNow.AddDays(-1);
        var todayStr = BusinessClock.Today.ToString("yyyyMMdd");

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

    private async Task RemindApprovalFlowsAsync(
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
            var overdueNodeIds = OverdueCurrentNodeIds(
                flow.CurrentNodeIds, flow.BpmnTokens, flow.ApplyTime, threshold);
            if (overdueNodeIds.Count == 0)
                continue;

            if (!workflowMap.TryGetValue(flow.WorkflowId, out var wf) ||
                string.IsNullOrEmpty(wf.BpmnXml)) continue;

            var process = BpmnParser.Parse(wf.BpmnXml);
            var approverIds = await ResolveApproversForFlowAsync(db, flow, process, overdueNodeIds);

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

    private async Task RemindMaterialFlowsAsync(
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
            var overdueNodeIds = OverdueCurrentNodeIds(
                flow.CurrentNodeIds, flow.BpmnTokens, flow.ApplyTime, threshold);
            if (overdueNodeIds.Count == 0)
                continue;

            if (!workflowMap.TryGetValue(flow.WorkflowId, out var wf) ||
                string.IsNullOrEmpty(wf.BpmnXml)) continue;

            var process = BpmnParser.Parse(wf.BpmnXml);
            var approverIds = await ResolveApproversForMaterialFlowAsync(db, flow, process, overdueNodeIds);

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

    private async Task<List<int>> ResolveApproversForFlowAsync(
        AppDbContext db, ApprovalFlow flow, BpmnProcess process, IReadOnlyCollection<string> nodeIds)
    {
        var result = new List<int>();
        foreach (var nodeId in nodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) ||
                token.Status != BpmnTokenStatus.Active) continue;

            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;

            var ids = token.SignStates is { Count: > 0 }
                ? await ResolvePendingSignStateUserIdsAsync(db, token)
                : await ResolveAssigneeAsync(
                    db, node, flow.ApplicantId, flow.TransfereeId, flow.BizType);
            foreach (var id in ids)
                if (!result.Contains(id)) result.Add(id);
        }
        return result;
    }

    private static List<string> OverdueCurrentNodeIds(
        IEnumerable<string> currentNodeIds,
        IReadOnlyDictionary<string, BpmnToken> tokens,
        DateTime fallbackApplyTime,
        DateTime threshold)
    {
        var result = new List<string>();
        foreach (var nodeId in currentNodeIds)
        {
            if (!tokens.TryGetValue(nodeId, out var token) || token.Status != BpmnTokenStatus.Active)
                continue;

            var startedAt = token.StartedAt ?? fallbackApplyTime;
            if (startedAt < threshold)
                result.Add(nodeId);
        }
        return result;
    }

    private async Task<List<int>> ResolveApproversForMaterialFlowAsync(
        AppDbContext db, MaterialFlow flow, BpmnProcess process, IReadOnlyCollection<string> nodeIds)
    {
        var result = new List<int>();
        foreach (var nodeId in nodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) ||
                token.Status != BpmnTokenStatus.Active) continue;

            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;

            var ids = token.SignStates is { Count: > 0 }
                ? await ResolvePendingSignStateUserIdsAsync(db, token)
                : await ResolveAssigneeAsync(db, node, flow.ApplicantId);
            foreach (var id in ids)
                if (!result.Contains(id)) result.Add(id);
        }
        return result;
    }

    private async Task<List<int>> ResolveAssigneeAsync(
        AppDbContext db,
        BpmnNode node,
        int applicantId,
        int? transfereeId = null,
        string? bizType = null)
    {
        var result = new List<int>();
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
            {
                foreach (var uid in await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(
                             db, applicantId, assignee))
                    if (!result.Contains(uid)) result.Add(uid);
            }
            else if (assignee == "deptManager")
            {
                var targetUserId = bizType == "transfer" && node.Id == "Task_receiver" && transfereeId.HasValue
                    ? transfereeId.Value
                    : applicantId;
                var targetUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetUserId);
                if (targetUser?.DepartmentId is not null)
                {
                    var dept = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetUser.DepartmentId.Value);
                    if (dept?.ManagerId is int managerId && managerId != applicantId &&
                        await db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
                        result.Add(managerId);
                    var deptAdmins = await db.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .Where(u => u.Id != applicantId && u.IsActive && u.DepartmentId == targetUser.DepartmentId &&
                                    u.UserRoles.Any(ur => ur.Role != null && ur.Role.IsActive && ur.Role.Code == "supervisor"))
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
            else
            {
                await AddResolvedUsersAsync(db, assignee, result, node.Id);
            }
        }

        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                await AddResolvedUsersAsync(db, part, result, node.Id);
            }
        }

        if (!string.IsNullOrEmpty(candidateGroups))
        {
            foreach (var group in candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(db, group);
                if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
                {
                    _logger.LogWarning("跳过审批节点 {NodeId} 的歧义角色配置：{Diagnostic}", node.Id, resolution.Diagnostic);
                    continue;
                }
                foreach (var uid in resolution.UserIds)
                    if (!result.Contains(uid)) result.Add(uid);
            }
        }

        return result;
    }

    private async Task AddResolvedUsersAsync(
        AppDbContext db,
        string identity,
        List<int> result,
        string nodeId)
    {
        var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(db, identity);
        if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
        {
            _logger.LogWarning("跳过审批节点 {NodeId} 的歧义人员配置：{Diagnostic}", nodeId, resolution.Diagnostic);
            return;
        }
        foreach (var uid in resolution.UserIds)
            if (!result.Contains(uid)) result.Add(uid);
    }

    private static async Task<List<int>> ResolvePendingSignStateUserIdsAsync(
        AppDbContext db,
        BpmnToken token)
    {
        var pendingUserIds = token.SignStates!
            .Where(x => !x.Value && int.TryParse(x.Key, out _))
            .Select(x => int.Parse(x.Key))
            .Distinct()
            .ToArray();
        return await db.Users.AsNoTracking()
            .Where(x => x.IsActive && pendingUserIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();
    }

    private static async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(AppDbContext db, int applicantId)
    {
        var result = new List<int>();
        var applicant = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicantId);
        if (applicant?.DepartmentId is not null)
        {
            var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
            if (department?.ManagerId is int managerId && managerId != applicantId &&
                await db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
            {
                result.Add(managerId);
            }
        }

        // 与正式审批权限解析保持一致：组织负责人优先，旧库未配置负责人时再兼容直属上级字段。
        if (result.Count == 0 && applicant?.SupervisorId is int supervisorId && supervisorId != applicantId &&
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == supervisorId && x.IsActive))
        {
            result.Add(supervisorId);
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
