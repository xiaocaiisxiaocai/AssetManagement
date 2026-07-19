using System.Globalization;
using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Reports;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AssetManagement.Infrastructure.Reports;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReportService(AppDbContext db, INotificationService notifications, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _notifications = notifications;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AssetSummaryDto> GetSummaryAsync()
    {
        var assets = ApplyAssetScope(_db.Assets.Where(x => !x.IsDeleted));

        // 优化：使用数据库聚合而非全部加载到内存
        var total = await assets.CountAsync();
        var available = await assets.CountAsync(x => x.Status == AssetStatus.Available);
        var borrowed = await assets.CountAsync(x => x.Status == AssetStatus.Borrowed);

        // 按分类汇总（使用 GroupBy + Join 避免N+1）
        var byCategory = await assets
            .GroupBy(x => x.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Total = g.Count(),
                Available = g.Count(x => x.Status == AssetStatus.Available),
                Borrowed = g.Count(x => x.Status == AssetStatus.Borrowed)
            })
            .Join(_db.AssetCategories.Where(x => !x.IsDeleted), x => x.CategoryId, c => c.Id, (x, c) => new CategoryStatRow
            {
                CategoryId = c.Id,
                CategoryCode = c.Code,
                Total = x.Total,
                Available = x.Available,
                Borrowed = x.Borrowed,
                Percent = total == 0 ? 0 : decimal.Round(x.Total * 100m / total, 2)
            })
            .OrderBy(x => x.CategoryCode)
            .ToListAsync();

        // 按部门汇总（仅汇总一级部门，简化计算）
        var byDept = await assets
            .Where(x => x.DepartmentId.HasValue)
            .GroupBy(x => x.DepartmentId!.Value)
            .Select(g => new
            {
                DepartmentId = g.Key,
                Total = g.Count(),
                Available = g.Count(x => x.Status == AssetStatus.Available),
                Borrowed = g.Count(x => x.Status == AssetStatus.Borrowed)
            })
            .Join(_db.Departments, x => x.DepartmentId, d => d.Id, (x, d) => new DeptStatRow
            {
                DepartmentId = d.Id,
                DepartmentName = d.Name,
                Total = x.Total,
                Available = x.Available,
                Borrowed = x.Borrowed,
                Percent = total == 0 ? 0 : decimal.Round(x.Total * 100m / total, 2)
            })
            .OrderBy(x => x.DepartmentName)
            .ToListAsync();

        return new AssetSummaryDto
        {
            Total = total,
            Available = available,
            Borrowed = borrowed,
            ByCategory = byCategory,
            ByDept = byDept
        };
    }

    public async Task<byte[]> ExportSummaryAsync()
    {
        var summary = await GetSummaryAsync();
        var rows = new List<string[]>
        {
            new[] { "统计项", "总数", "可用", "借出" },
            new[] { "全部资产", summary.Total.ToString(), summary.Available.ToString(), summary.Borrowed.ToString() },
            Array.Empty<string>(),
            new[] { "按分类", "总数", "可用", "借出", "占比" }
        };
        rows.AddRange(summary.ByCategory.Select(x => new[]
        {
            x.CategoryCode,
            x.Total.ToString(),
            x.Available.ToString(),
            x.Borrowed.ToString(),
            $"{x.Percent:0.##}%"
        }));
        rows.Add(Array.Empty<string>());
        rows.Add(new[] { "按部门", "总数", "可用", "借出", "占比" });
        rows.AddRange(summary.ByDept.Select(x => new[]
        {
            x.DepartmentName,
            x.Total.ToString(),
            x.Available.ToString(),
            x.Borrowed.ToString(),
            $"{x.Percent:0.##}%"
        }));
        return XlsxTable.Write(rows);
    }

    public async Task<PagedResult<BorrowReportRow>> QueryBorrowedAsync(BorrowReportQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var flows = ApplyBorrowQuery(_db.ApprovalFlows.AsNoTracking(), query);
        var total = await flows.CountAsync();
        var pageFlows = await flows
            .OrderByDescending(x => x.ApplyTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<BorrowReportRow>
        {
            Items = await ToBorrowRows(pageFlows),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<byte[]> ExportBorrowedAsync(BorrowReportQuery query)
    {
        var rows = new List<string[]>
        {
            new[] { "流程号", "资产编号", "资产名称", "分类", "借用人", "部门", "申请时间", "预计归还", "状态" }
        };
        var flows = await ApplyBorrowQuery(_db.ApprovalFlows.AsNoTracking(), query)
            .OrderByDescending(x => x.ApplyTime)
            .ToListAsync();
        rows.AddRange((await ToBorrowRows(flows)).Select(x => new[]
        {
            x.FlowNo,
            x.AssetNo,
            x.AssetName,
            x.CategoryCode,
            x.Borrower,
            x.BorrowerDept ?? "",
            x.ApplyTime.ToString("yyyy-MM-dd HH:mm"),
            x.ReturnDate ?? "",
            x.Status
        }));
        return XlsxTable.Write(rows);
    }

    public async Task<List<OverdueReportRow>> QueryOverdueAsync()
    {
        var flows = await ApplyFlowScope(_db.ApprovalFlows.AsNoTracking())
            .Where(x => x.BizType == "borrow" && x.Status == "approved" && x.ReturnDate != null)
            .OrderByDescending(x => x.ApplyTime)
            .ToListAsync();
        var assetIds = flows.Select(x => x.AssetId).Distinct().ToArray();
        var borrowedAssets = await ApplyAssetScope(_db.Assets.AsNoTracking())
            .Where(x => assetIds.Contains(x.Id) && !x.IsDeleted && x.Status == AssetStatus.Borrowed)
            .ToDictionaryAsync(x => x.Id);
        var today = BusinessClock.Today;
        var overdue = flows
            .Where(x => borrowedAssets.ContainsKey(x.AssetId))
            .Select(x => new { Flow = x, Due = ParseDate(x.ReturnDate) })
            .Where(x => x.Due.HasValue && x.Due.Value.Date < today)
            .Select(x => new { x.Flow, Due = x.Due!.Value.Date, Days = (today - x.Due.Value.Date).Days })
            .ToList();

        return await ToOverdueRows(overdue.Select(x => (x.Flow, x.Due, x.Days)).ToList());
    }

    public async Task<byte[]> ExportOverdueAsync()
    {
        var rows = new List<string[]>
        {
            new[] { "资产编号", "资产名称", "分类", "借用人", "部门", "预计归还", "逾期天数", "严重逾期" }
        };
        rows.AddRange((await QueryOverdueAsync()).Select(x => new[]
        {
            x.AssetNo,
            x.AssetName,
            x.CategoryCode,
            x.Borrower,
            x.BorrowerDept ?? "",
            x.ReturnDate,
            x.OverdueDays.ToString(),
            x.IsSerious ? "是" : "否"
        }));
        return XlsxTable.Write(rows);
    }

    public async Task RemindOverdueAsync(int assetId, int? userId)
    {
        var row = (await QueryOverdueAsync()).FirstOrDefault(x => x.AssetId == assetId)
            ?? throw new BizException(4060, "资产不存在或未逾期");
        var (auditLog, notification) = BuildOverdueReminder(row, userId);
        _db.AuditLogs.Add(auditLog);
        await _notifications.CreateAsync(notification);
        // 幂等通知已存在时 NotificationService 会直接返回，仍需显式保存本次催办审计。
        await _db.SaveChangesAsync();
    }

    public async Task<int> RemindOverdueBatchAsync(IReadOnlyCollection<int> assetIds, int? userId)
    {
        var ids = assetIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            throw new BizException(4001, "请选择需要催办的逾期资产");
        }
        if (ids.Length > AppConstants.MaxPageSize)
        {
            throw new BizException(4001, $"单次最多催办 {AppConstants.MaxPageSize} 项");
        }

        // 任何无效 ID 都必须在产生审计或通知之前失败，避免半批成功。
        var overdueRows = await QueryOverdueAsync();
        var rowMap = overdueRows
            .Where(row => ids.Contains(row.AssetId))
            .GroupBy(row => row.AssetId)
            .ToDictionary(group => group.Key, group => group.First());
        var invalidIds = ids.Where(id => !rowMap.ContainsKey(id)).ToArray();
        if (invalidIds.Length > 0)
        {
            throw new BizException(4060, $"以下资产不存在或未逾期：{string.Join("、", invalidIds)}");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var reminders = ids.Select(id => BuildOverdueReminder(rowMap[id], userId)).ToList();
        _db.AuditLogs.AddRange(reminders.Select(x => x.AuditLog));
        await _notifications.CreateBatchAsync(reminders.Select(x => x.Notification));
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return ids.Length;
    }

    private static (AuditLog AuditLog, CreateNotificationRequest Notification) BuildOverdueReminder(
        OverdueReportRow row,
        int? userId)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            ActionType = "remind",
            TargetType = "asset",
            TargetId = row.AssetId.ToString(CultureInfo.InvariantCulture),
            Summary = $"逾期催办：{row.AssetNo} {row.AssetName}",
            Detail = $"借用人：{row.Borrower}；预计归还：{row.ReturnDate}；逾期：{row.OverdueDays}天",
            OccurredAt = DateTime.UtcNow
        };
        var notification = new CreateNotificationRequest
        {
            Type = "overdue",
            Title = $"逾期催办：{row.AssetNo} {row.AssetName}",
            Body = $"您借用的 {row.AssetName}（{row.AssetNo}）预计归还日期为 {row.ReturnDate}，已逾期 {row.OverdueDays} 天，请尽快归还。",
            FlowId = row.FlowId,
            UserId = row.BorrowerId,
            IdempotencyKey = $"overdue_remind_{row.FlowId}_{BusinessClock.Today:yyyyMMdd}_{userId ?? 0}"
        };
        return (auditLog, notification);
    }

    private IQueryable<ApprovalFlow> ApplyBorrowQuery(IQueryable<ApprovalFlow> queryable, BorrowReportQuery query)
    {
        queryable = ApplyFlowScope(queryable).Where(x =>
            x.BizType == "borrow"
            && x.Status == "approved"
            && _db.Assets.Any(a => a.Id == x.AssetId && !a.IsDeleted));
        if (query.StartTime.HasValue)
        {
            queryable = queryable.Where(x => x.ApplyTime >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            var end = query.EndTime.Value;
            queryable = end.TimeOfDay == TimeSpan.Zero
                ? queryable.Where(x => x.ApplyTime < end.Date.AddDays(1))
                : queryable.Where(x => x.ApplyTime <= end);
        }

        if (query.BorrowerId.HasValue)
        {
            queryable = queryable.Where(x => x.ApplicantId == query.BorrowerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            queryable = status switch
            {
                "returned" => queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId && !a.IsDeleted && a.Status == AssetStatus.Available)),
                "borrowed" => queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId && !a.IsDeleted && a.Status == AssetStatus.Borrowed)),
                _ => queryable
            };
        }

        if (query.CategoryId.HasValue)
        {
            queryable = queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId && !a.IsDeleted && a.CategoryId == query.CategoryId.Value));
        }

        return queryable;
    }

    private async Task<List<BorrowReportRow>> ToBorrowRows(IEnumerable<ApprovalFlow> flows)
    {
        var list = flows.ToList();
        var assetIds = list.Select(x => x.AssetId).Distinct().ToArray();
        var assets = await ApplyAssetScope(_db.Assets.AsNoTracking())
            .Where(x => assetIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id);
        var categoryIds = assets.Values.Select(x => x.CategoryId).Distinct().ToArray();
        var categories = await _db.AssetCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id) && !x.IsDeleted).ToDictionaryAsync(x => x.Id);

        return list.Select(x =>
        {
            assets.TryGetValue(x.AssetId, out var asset);
            var category = asset is not null && categories.TryGetValue(asset.CategoryId, out var c) ? c : null;
            return new BorrowReportRow
            {
                FlowId = x.Id,
                FlowNo = x.FlowNo,
                AssetId = x.AssetId,
                AssetNo = x.AssetNo,
                AssetName = x.AssetName,
                CategoryCode = category?.Code ?? "",
                BorrowerId = x.ApplicantId,
                Borrower = x.Applicant,
                BorrowerDept = x.ApplicantDept,
                ReturnDate = x.ReturnDate,
                ApplyTime = x.ApplyTime,
                Status = asset?.Status == AssetStatus.Available ? "returned" : "borrowed"
            };
        }).ToList();
    }

    private async Task<List<OverdueReportRow>> ToOverdueRows(List<(ApprovalFlow Flow, DateTime Due, int Days)> overdue)
    {
        var assetIds = overdue.Select(x => x.Flow.AssetId).Distinct().ToArray();
        var assets = await ApplyAssetScope(_db.Assets.AsNoTracking())
            .Where(x => assetIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id);
        var categoryIds = assets.Values.Select(x => x.CategoryId).Distinct().ToArray();
        var categories = await _db.AssetCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id) && !x.IsDeleted).ToDictionaryAsync(x => x.Id);

        return overdue.Select(x =>
        {
            assets.TryGetValue(x.Flow.AssetId, out var asset);
            var category = asset is not null && categories.TryGetValue(asset.CategoryId, out var c) ? c : null;
            return new OverdueReportRow
            {
                FlowId = x.Flow.Id,
                AssetId = x.Flow.AssetId,
                AssetNo = x.Flow.AssetNo,
                AssetName = x.Flow.AssetName,
                CategoryCode = category?.Code ?? "",
                BorrowerId = x.Flow.ApplicantId,
                Borrower = x.Flow.Applicant,
                BorrowerDept = x.Flow.ApplicantDept,
                ReturnDate = x.Due.ToString("yyyy-MM-dd"),
                OverdueDays = x.Days,
                IsSerious = x.Days > 10
            };
        }).ToList();
    }

    private static CategoryStatRow ToCategoryRow(AssetCategory category, IEnumerable<Asset> assets, int total)
    {
        var list = assets.ToList();
        return new CategoryStatRow
        {
            CategoryId = category.Id,
            CategoryCode = category.Code,
            Total = list.Count,
            Available = list.Count(x => x.Status == AssetStatus.Available),
            Borrowed = list.Count(x => x.Status == AssetStatus.Borrowed),
            Percent = Percent(list.Count, total)
        };
    }

    private static DeptStatRow ToDeptRow(Department department, IEnumerable<Asset> assets, int total)
    {
        var list = assets.ToList();
        return new DeptStatRow
        {
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            Total = list.Count,
            Available = list.Count(x => x.Status == AssetStatus.Available),
            Borrowed = list.Count(x => x.Status == AssetStatus.Borrowed),
            Percent = Percent(list.Count, total)
        };
    }

    private static Department RootDepartment(Department department, List<Department> departments)
    {
        var current = department;
        var visited = new HashSet<int> { current.Id };
        while (current.ParentId.HasValue)
        {
            var parent = departments.FirstOrDefault(x => x.Id == current.ParentId.Value);
            if (parent is null || !visited.Add(parent.Id))
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    private static DateTime? ParseDate(string? text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, out var date) ? date.Date : null;

    private static decimal Percent(int count, int total)
        => total == 0 ? 0 : decimal.Round(count * 100m / total, 2);

    private IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> queryable)
    {
        var allowedDepartmentIds = AllowedDepartmentIds();
        return allowedDepartmentIds is null
            ? queryable
            : queryable.Where(x => x.DepartmentId.HasValue && allowedDepartmentIds.Contains(x.DepartmentId.Value));
    }

    private IQueryable<ApprovalFlow> ApplyFlowScope(IQueryable<ApprovalFlow> queryable)
    {
        var allowedDepartmentIds = AllowedDepartmentIds();
        return allowedDepartmentIds is null
            ? queryable
            : queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId
                && !a.IsDeleted
                && a.DepartmentId.HasValue
                && allowedDepartmentIds.Contains(a.DepartmentId.Value)));
    }

    // null 表示共享资产池不受限；空数组表示部门主管配置不完整，必须 fail-closed。
    private int[]? AllowedDepartmentIds()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("admin") || !roles.Contains("supervisor"))
        {
            return null;
        }

        if (!int.TryParse(principal.FindFirst("departmentId")?.Value, out var rootDepartmentId))
        {
            return Array.Empty<int>();
        }

        // 历史部门停用后，主管仍须能够处理其后代部门的归还和报表数据。
        var departments = _db.Departments.AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToList();
        if (!departments.Any(x => x.Id == rootDepartmentId))
        {
            return Array.Empty<int>();
        }

        var result = new HashSet<int> { rootDepartmentId };
        var queue = new Queue<int>();
        queue.Enqueue(rootDepartmentId);
        while (queue.TryDequeue(out var parentId))
        {
            foreach (var childId in departments.Where(x => x.ParentId == parentId).Select(x => x.Id))
            {
                if (result.Add(childId)) queue.Enqueue(childId);
            }
        }

        return result.ToArray();
    }
}
