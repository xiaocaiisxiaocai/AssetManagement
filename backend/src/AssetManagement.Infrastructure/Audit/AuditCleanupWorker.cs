using AssetManagement.Application.Audit;
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
            var (enabled, retentionDays, nextRun) = await LoadScheduleAsync();
            await DelayUntil(nextRun, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;
            if (!enabled) continue;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuditMaintenanceService>();
                var result = await service.CleanupAsync(retentionDays);
                _logger.LogInformation("定时清理审计日志完成，保留 {Days} 天，删除 {Count} 条",
                    result.RetentionDays, result.DeletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时清理审计日志异常");
            }
        }
    }

    private async Task<(bool Enabled, int RetentionDays, DateTime NextRun)> LoadScheduleAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SystemSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith("audit_cleanup_") || x.Key == "audit_retention_days")
            .ToDictionaryAsync(x => x.Key, x => x.Value);

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
        var next = DateTime.Now.Date.Add(time);
        return next <= DateTime.Now ? next.AddDays(1) : next;
    }

    private static async Task DelayUntil(DateTime nextRun, CancellationToken ct)
    {
        var delay = nextRun - DateTime.Now;
        if (delay <= TimeSpan.Zero) delay = TimeSpan.FromMinutes(1);
        await Task.Delay(delay, ct).ContinueWith(_ => { });
    }
}
