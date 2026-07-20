using AssetManagement.Application.Common;

namespace AssetManagement.Application.Audit;

public record AuditLogDto
{
    public int Id { get; init; }
    public int? UserId { get; init; }
    public string? UserName { get; init; }
    public string ActionType { get; init; } = "";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string Summary { get; init; } = "";
    public string? Detail { get; init; }
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public int? DurationMs { get; init; }
    public DateTime OccurredAt { get; init; }
}

public record AuditLogQuery
{
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int? UserId { get; init; }
    public string? ActionType { get; init; }
    public string? Module { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record AuditCleanupPreviewDto
{
    public int RetentionDays { get; init; }
    public DateTime CutoffTime { get; init; }
    public int DeleteCount { get; init; }
}

public record AuditCleanupResultDto
{
    public int RetentionDays { get; init; }
    public DateTime CutoffTime { get; init; }
    public int DeletedCount { get; init; }
}

public record DatabaseBackupResultDto
{
    public string FileName { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}

public record DatabaseBackupFileDto
{
    public string FileName { get; init; } = "";
    public string FileType { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}

public record DatabaseBackupDownloadDto(Stream Stream, string FileName, string ContentType);

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> QueryAsync(AuditLogQuery query);
    Task<byte[]> ExportAsync(AuditLogQuery query);
}

public interface IAuditMaintenanceService
{
    Task<AuditCleanupPreviewDto> PreviewCleanupAsync(
        int retentionDays,
        CancellationToken cancellationToken = default);
    Task<AuditCleanupResultDto> CleanupAsync(
        int retentionDays,
        int? operatorUserId = null,
        CancellationToken cancellationToken = default);
}

public interface IDatabaseBackupService
{
    Task<DatabaseBackupResultDto> BackupAsync(CancellationToken cancellationToken = default);
    Task<List<DatabaseBackupFileDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<DatabaseBackupDownloadDto?> OpenAsync(string fileName, CancellationToken cancellationToken = default);
}
