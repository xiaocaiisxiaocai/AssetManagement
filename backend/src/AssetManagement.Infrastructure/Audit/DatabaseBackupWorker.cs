using AssetManagement.Application.Audit;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Audit;

public class DatabaseBackupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackupWorker> _logger;

    public DatabaseBackupWorker(IServiceScopeFactory scopeFactory, ILogger<DatabaseBackupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (enabled, nextRun) = await LoadScheduleAsync();
            await DelayUntil(nextRun, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;
            if (!enabled) continue;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
                var result = await service.BackupAsync(stoppingToken);
                _logger.LogInformation("定时数据库备份完成：{FilePath}", result.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时数据库备份异常");
            }
        }
    }

    private async Task<(bool Enabled, DateTime NextRun)> LoadScheduleAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SystemSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith("database_backup_"))
            .ToDictionaryAsync(x => x.Key, x => x.Value);

        var enabled = !settings.TryGetValue("database_backup_enabled", out var enabledValue)
            || enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        var time = settings.TryGetValue("database_backup_time", out var timeValue)
            ? timeValue
            : "02:00";
        return (enabled, NextRun(time));
    }

    private static DateTime NextRun(string timeText)
    {
        if (!TimeSpan.TryParse(timeText, out var time)) time = new TimeSpan(2, 0, 0);
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
