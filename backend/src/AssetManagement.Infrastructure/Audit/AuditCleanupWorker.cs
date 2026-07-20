using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Audit;

public class AuditCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditCleanupWorker> _logger;

    public AuditCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<AuditCleanupWorker> logger)
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
                var (enabled, retentionDays, nextRun) = await LoadScheduleAsync(stoppingToken);
                await DelayUntil(nextRun, stoppingToken);
                if (!enabled) continue;

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuditMaintenanceService>();
                var result = await service.CleanupAsync(retentionDays, cancellationToken: stoppingToken);
                _logger.LogInformation("定时清理审计日志完成，保留 {Days} 天，删除 {Count} 条",
                    result.RetentionDays, result.DeletedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时清理审计日志轮询异常，将在 1 分钟后重试");
                await DelayAfterFailure(stoppingToken);
            }
        }
    }

    private async Task<(bool Enabled, int RetentionDays, DateTime NextRun)> LoadScheduleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SystemSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith("audit_cleanup_") || x.Key == "audit_retention_days")
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct);

        var enabled = !settings.TryGetValue("audit_cleanup_enabled", out var enabledValue)
            || enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        var retentionDays = settings.TryGetValue("audit_retention_days", out var daysValue)
            && int.TryParse(daysValue, out var days)
            ? days
            : 30;
        if (retentionDays is not (7 or 14 or 30)) retentionDays = 30;
        var time = settings.TryGetValue("audit_cleanup_time", out var timeValue)
            ? timeValue
            : "02:10";
        return (enabled, retentionDays, NextRun(time));
    }

    private static DateTime NextRun(string timeText)
    {
        if (!TimeSpan.TryParse(timeText, out var time)) time = new TimeSpan(2, 10, 0);
        var now = BusinessClock.Now;
        var next = now.Date.Add(time);
        return next <= now ? next.AddDays(1) : next;
    }

    private static async Task DelayUntil(DateTime nextRun, CancellationToken ct)
    {
        var delay = nextRun - BusinessClock.Now;
        if (delay <= TimeSpan.Zero) delay = TimeSpan.FromMinutes(1);
        await Task.Delay(delay, ct);
    }

    private static async Task DelayAfterFailure(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 宿主停止时立即结束重试等待。
        }
    }
}
