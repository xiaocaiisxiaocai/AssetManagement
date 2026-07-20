using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Notifications;

public class OverdueNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueNotificationWorker> _logger;

    public OverdueNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<OverdueNotificationWorker> logger)
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
                await WaitUntilMidnight(stoppingToken);
                await ScanAndNotifyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "到期提醒扫描异常");
            }
        }
    }

    private async Task WaitUntilMidnight(CancellationToken ct)
    {
        var now = BusinessClock.Now;
        var nextRun = now.Date.AddDays(1); // 次日 00:00
        var delay = nextRun - now;
        if (delay <= TimeSpan.Zero) delay = TimeSpan.FromMinutes(1);
        await Task.Delay(delay, ct);
    }

    internal async Task ScanAndNotifyAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = BusinessClock.TodayDateOnly;

        // 查询所有审批通过、未入库、有归还日期的借用流程（排除已删除资产）
        var flows = await db.ApprovalFlows
            .Where(f => f.BizType == "borrow"
                     && f.Status == "approved"
                     && f.ConfirmedAt == null
                     && f.ReturnDate != null
                     && db.Assets.Any(a => a.Id == f.AssetId &&
                         !a.IsDeleted && a.Status == AssetStatus.Borrowed))
            .Select(f => new
            {
                f.Id,
                f.ApplicantId,
                CurrentCustodianId = db.Assets
                    .Where(a => a.Id == f.AssetId)
                    .Select(a => a.CustodianId)
                    .FirstOrDefault(),
                f.ReturnDate,
                f.AssetName,
                f.AssetNo
            })
            .ToListAsync(ct);

        var notifications = new List<CreateNotificationRequest>();
        var todayStr = today.ToString("yyyyMMdd");

        foreach (var flow in flows)
        {
            if (!DateOnly.TryParse(flow.ReturnDate, out var returnDate)) continue;

            var daysLeft = (returnDate.ToDateTime(TimeOnly.MinValue) - BusinessClock.Today).Days;

            string? type = daysLeft switch
            {
                < 0 => "overdue",
                1 => "due_soon_1d",
                3 => "due_soon_3d",
                _ => null
            };

            if (type == null) continue;

            var key = $"{type}_{flow.Id}_{todayStr}";
            var (title, body) = BuildMessage(type, flow.AssetName, flow.AssetNo, returnDate);
            notifications.Add(new CreateNotificationRequest
            {
                Type = type,
                Title = title,
                Body = body,
                FlowId = flow.Id,
                UserId = flow.CurrentCustodianId ?? flow.ApplicantId,
                IdempotencyKey = key,
            });
        }

        if (notifications.Count > 0)
        {
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var insertedCount = await notificationService.CreateBatchAsync(notifications, ct);
            _logger.LogInformation("生成到期提醒 {Count} 条", insertedCount);
        }
    }

    private static (string title, string body) BuildMessage(string type, string assetName, string assetNo, DateOnly returnDate)
    {
        var dateStr = returnDate.ToString("yyyy-MM-dd");
        return type switch
        {
            "overdue" => (
                $"借用逾期：{assetName}",
                $"资产 {assetNo}（{assetName}）归还日期 {dateStr} 已过，请尽快归还。"),
            "due_soon_1d" => (
                $"明日到期：{assetName}",
                $"资产 {assetNo}（{assetName}）将于明天（{dateStr}）到期，请及时安排归还。"),
            "due_soon_3d" => (
                $"3天后到期：{assetName}",
                $"资产 {assetNo}（{assetName}）将于 {dateStr} 到期，请提前安排归还。"),
            _ => ("借用提醒", $"资产 {assetNo} 归还日期为 {dateStr}。")
        };
    }
}
