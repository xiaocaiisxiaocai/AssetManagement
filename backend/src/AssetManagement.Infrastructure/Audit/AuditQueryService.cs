using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
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
        var (page, pageSize) = Pagination.Normalize(query.Page, query.PageSize);
        var logs = ApplyQuery(_db.AuditLogs.AsNoTracking(), query);
        var total = await logs.CountAsync();
        var offset = Pagination.GetOffset(page, pageSize, total);
        var items = offset.HasValue
            ? await logs.OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.Id)
                .Skip(offset.Value)
                .Take(pageSize)
                .ToListAsync()
            : [];

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
            new[] { "时间", "操作人", "操作类型", "模块", "目标ID", "摘要", "IP", "客户端", "耗时(ms)" }
        };
        var exportQuery = ApplyQuery(_db.AuditLogs.AsNoTracking(), query);
        if (await exportQuery.CountAsync() > AppConstants.MaxExportRows)
            throw new BizException(4130, $"导出数据不能超过 {AppConstants.MaxExportRows} 行，请缩小筛选范围");
        var logs = await exportQuery
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
            x.Ip ?? "",
            x.UserAgent ?? "",
            x.DurationMs?.ToString() ?? ""
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
            if (actionType == "soft_delete")
            {
                queryable = queryable.Where(x => x.ActionType == "soft_delete"
                    || (x.ActionType == "DELETE"
                        && (x.TargetType == "Asset"
                            || x.TargetType == "AssetCategory"
                            || x.TargetType == "TestMaterial"
                            || x.TargetType == "TestProject")
                        && !x.Summary.Contains("/purge")));
            }
            else if (actionType == "purge")
            {
                queryable = queryable.Where(x => x.ActionType == "purge"
                    || (x.ActionType == "DELETE" && x.Summary.Contains("/purge")));
            }
            else
            {
                queryable = queryable.Where(x => x.ActionType == actionType);
            }
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
            ActionType = ResolveActionType(x),
            TargetType = x.TargetType,
            TargetId = x.TargetId,
            Summary = x.Summary,
            Detail = x.Detail,
            Ip = IpNormalizer.Normalize(x.Ip),
            UserAgent = x.UserAgent,
            DurationMs = x.DurationMs,
            OccurredAt = x.OccurredAt
        }).ToList();
    }

    private static string ResolveActionType(AuditLog log)
    {
        if (!string.Equals(log.ActionType, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return log.ActionType;
        }

        if (log.Summary.Contains("/purge", StringComparison.OrdinalIgnoreCase))
        {
            return "purge";
        }

        return log.TargetType is "Asset" or "AssetCategory" or "TestMaterial" or "TestProject"
            ? "soft_delete"
            : log.ActionType;
    }
}
