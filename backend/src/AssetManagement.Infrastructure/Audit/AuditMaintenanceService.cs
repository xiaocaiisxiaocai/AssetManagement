using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Audit;

public class AuditMaintenanceService : IAuditMaintenanceService
{
    private static readonly int[] AllowedRetentionDays = [7, 14, 30];
    private readonly AppDbContext _db;

    public AuditMaintenanceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuditCleanupPreviewDto> PreviewCleanupAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        ValidateRetentionDays(retentionDays);
        var cutoff = CutoffTime(retentionDays);
        var count = await _db.AuditLogs.CountAsync(x => x.OccurredAt < cutoff, cancellationToken);
        return new AuditCleanupPreviewDto
        {
            RetentionDays = retentionDays,
            CutoffTime = cutoff,
            DeleteCount = count
        };
    }

    public async Task<AuditCleanupResultDto> CleanupAsync(
        int retentionDays,
        int? operatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRetentionDays(retentionDays);
        var cutoff = CutoffTime(retentionDays);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var deletedCount = await _db.AuditLogs
            .Where(x => x.OccurredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = operatorUserId,
            ActionType = "cleanup",
            TargetType = "AuditLog",
            Summary = $"清理审计日志：保留 {retentionDays} 天，删除 {deletedCount} 条",
            Detail = $"{{\"retentionDays\":{retentionDays},\"cutoffTime\":\"{cutoff:O}\",\"deletedCount\":{deletedCount}}}",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuditCleanupResultDto
        {
            RetentionDays = retentionDays,
            CutoffTime = cutoff,
            DeletedCount = deletedCount
        };
    }

    private static DateTime CutoffTime(int retentionDays)
        => BusinessClock.ToUtc(BusinessClock.Today.AddDays(-retentionDays));

    private static void ValidateRetentionDays(int retentionDays)
    {
        if (!AllowedRetentionDays.Contains(retentionDays))
        {
            throw new BizException(400, "审计日志保留天数只能选择 7、14、30 天");
        }
    }
}
