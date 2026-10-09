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
        var waitForSchedule = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (waitForSchedule)
                {
                    await WaitUntilNineAm(stoppingToken);
                }

                await ScanAndRemindAsync(stoppingToken);
                waitForSchedule = true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                waitForSchedule = false;
                _logger.LogError(ex, "待审批催办扫描异常，将在 15 分钟后重试");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
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

    internal async Task ScanAndRemindAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var threshold = DateTime.UtcNow.AddDays(-1);
        var todayStr = BusinessClock.Today.ToString("yyyyMMdd");

        var requests = new List<CreateNotificationRequest>();

        // 扫描资产审批流
        await RemindApprovalFlowsAsync(db, threshold, todayStr, requests, cancellationToken);

        // 扫描料件流转
        await RemindMaterialFlowsAsync(db, threshold, todayStr, requests, cancellationToken);

        if (requests.Count > 0)
        {
            await notificationSvc.CreateBatchAsync(requests, cancellationToken);
            _logger.LogInformation("发送待审批催办通知 {Count} 条", requests.Count);
        }
    }

    private async Task RemindApprovalFlowsAsync(
        AppDbContext db, DateTime threshold, string todayStr,
        List<CreateNotificationRequest> requests,
        CancellationToken cancellationToken)
    {
        var pendingFlows = await db.ApprovalFlows
            .Where(f => f.Status == "pending")
            .ToListAsync(cancellationToken);

        var workflowIds = pendingFlows.Select(f => f.WorkflowId).Distinct().ToArray();
        var workflowMap = await db.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w, cancellationToken);
        var assetIds = pendingFlows.Select(f => f.AssetId).Distinct().ToArray();
        var assetDepartments = assetIds.Length == 0
            ? new Dictionary<int, int?>()
            : await db.Assets.AsNoTracking()
                .Where(asset => assetIds.Contains(asset.Id))
                .ToDictionaryAsync(asset => asset.Id, asset => asset.DepartmentId, cancellationToken);

        foreach (var flow in pendingFlows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var overdueNodeIds = OverdueCurrentNodeIds(
                flow.CurrentNodeIds, flow.BpmnTokens, flow.ApplyTime, threshold);
            if (overdueNodeIds.Count == 0)
                continue;

            if (!workflowMap.TryGetValue(flow.WorkflowId, out var wf) ||
                string.IsNullOrEmpty(wf.BpmnXml)) continue;

            var process = BpmnParser.Parse(wf.BpmnXml);
            assetDepartments.TryGetValue(flow.AssetId, out var assetDepartmentId);
            var approverIds = await ResolveApproversForFlowAsync(
                db, flow, process, overdueNodeIds, assetDepartmentId);

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
        List<CreateNotificationRequest> requests,
        CancellationToken cancellationToken)
    {
        var pendingFlows = await db.MaterialFlows
            .Where(f => f.Status == "pending")
            .ToListAsync(cancellationToken);

        var workflowIds = pendingFlows.Where(f => f.WorkflowId.HasValue)
            .Select(f => f.WorkflowId!.Value).Distinct().ToArray();
        var workflowMap = await db.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w, cancellationToken);

        foreach (var flow in pendingFlows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var overdueNodeIds = OverdueCurrentNodeIds(
                flow.CurrentNodeIds, flow.BpmnTokens, flow.ApplyTime, threshold);
            if (overdueNodeIds.Count == 0)
                continue;

            if (!flow.WorkflowId.HasValue
                || !workflowMap.TryGetValue(flow.WorkflowId.Value, out var wf) ||
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
        AppDbContext db,
        ApprovalFlow flow,
        BpmnProcess process,
        IReadOnlyCollection<string> nodeIds,
        int? assetDepartmentId)
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
                : await UserTaskApproverResolver.ResolveAsync(
                    db, node, flow.ApplicantId, flow.TransfereeId, flow.BizType, assetDepartmentId);
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
                : await UserTaskApproverResolver.ResolveAsync(
                    db, node, flow.ApplicantId, flow.TransfereeId, flow.BizType);
            foreach (var id in ids)
                if (!result.Contains(id)) result.Add(id);
        }
        return result;
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

    private static string BizTypeLabel(string bizType) => bizType switch
    {
        "borrow" => "资产借用",
        "transfer" => "资产转让",
        "return" => "资产归还",
        _ => bizType
    };
}
