using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Audit;

public class AuditQueryService : IAuditQueryService
{
    private readonly AppDbContext _db;

    public AuditQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditLogDto>> QueryAsync(AuditLogQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var logs = ApplyQuery(_db.AuditLogs.AsNoTracking(), query);
        var total = await logs.CountAsync();
        var items = await logs
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AuditLogDto>
        {
            Items = await ToDtos(items),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<byte[]> ExportAsync(AuditLogQuery query)
    {
        var rows = new List<string[]>
        {
            new[] { "时间", "操作人", "操作类型", "模块", "目标ID", "摘要", "IP" }
        };
        var logs = await ApplyQuery(_db.AuditLogs.AsNoTracking(), query)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        rows.AddRange((await ToDtos(logs)).Select(x => new[]
        {
            x.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
            x.UserName ?? "",
            x.ActionType,
            x.TargetType ?? "",
            x.TargetId ?? "",
            x.Summary,
            x.Ip ?? ""
        }));
        return XlsxTable.Write(rows);
    }

    private IQueryable<AuditLog> ApplyQuery(IQueryable<AuditLog> queryable, AuditLogQuery query)
    {
        if (query.StartTime.HasValue)
        {
            queryable = queryable.Where(x => x.OccurredAt >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            queryable = queryable.Where(x => x.OccurredAt <= query.EndTime.Value);
        }

        if (query.UserId.HasValue)
        {
            queryable = queryable.Where(x => x.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ActionType))
        {
            var actionType = query.ActionType.Trim();
            queryable = queryable.Where(x => x.ActionType == actionType);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim();
            queryable = queryable.Where(x => x.TargetType == module || x.Summary.Contains(module));
        }

        return queryable;
    }

    private async Task<List<AuditLogDto>> ToDtos(IEnumerable<AuditLog> logs)
    {
        var list = logs.ToList();
        var userIds = list.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        return list.Select(x => new AuditLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.UserId.HasValue && users.TryGetValue(x.UserId.Value, out var name) ? name : null,
            ActionType = x.ActionType,
            TargetType = x.TargetType,
            TargetId = x.TargetId,
            Summary = x.Summary,
            Detail = x.Detail,
            Ip = x.Ip,
            OccurredAt = x.OccurredAt
        }).ToList();
    }
}
