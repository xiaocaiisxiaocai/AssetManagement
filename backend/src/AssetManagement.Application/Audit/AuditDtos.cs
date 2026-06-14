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

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> QueryAsync(AuditLogQuery query);
    Task<byte[]> ExportAsync(AuditLogQuery query);
}
