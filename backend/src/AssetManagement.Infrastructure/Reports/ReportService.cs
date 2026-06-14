using System.Globalization;
using AssetManagement.Application.Common;
using AssetManagement.Application.Reports;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Reports;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AssetSummaryDto> GetSummaryAsync()
    {
        var assets = await _db.Assets.AsNoTracking().ToListAsync();
        var categories = await _db.AssetCategories.AsNoTracking().ToListAsync();
        var departments = await _db.Departments.AsNoTracking().ToListAsync();
        var categoryById = categories.ToDictionary(x => x.Id);
        var rootDepartmentById = departments.ToDictionary(x => x.Id, x => RootDepartment(x, departments));
        var total = assets.Count;

        return new AssetSummaryDto
        {
            Total = total,
            Available = assets.Count(x => x.Status == AssetStatus.Available),
            Borrowed = assets.Count(x => x.Status == AssetStatus.Borrowed),
            Maintenance = assets.Count(x => x.Status == AssetStatus.Maintenance),
            Scrapped = assets.Count(x => x.Status == AssetStatus.Scrapped),
            TotalValue = assets.Sum(AssetValue),
            ByCategory = assets
                .Where(x => categoryById.ContainsKey(x.CategoryId))
                .GroupBy(x => categoryById[x.CategoryId])
                .OrderBy(x => x.Key.Code)
                .Select(x => ToCategoryRow(x.Key, x, total))
                .ToList(),
            ByDept = assets
                .Where(x => x.DepartmentId.HasValue && rootDepartmentById.ContainsKey(x.DepartmentId.Value))
                .GroupBy(x => rootDepartmentById[x.DepartmentId!.Value])
                .OrderBy(x => x.Key.Code)
                .Select(x => ToDeptRow(x.Key, x, total))
                .ToList()
        };
    }

    public async Task<byte[]> ExportSummaryAsync()
    {
        var summary = await GetSummaryAsync();
        var rows = new List<string[]>
        {
            new[] { "统计项", "总数", "可用", "借出", "金额" },
            new[] { "全部资产", summary.Total.ToString(), summary.Available.ToString(), summary.Borrowed.ToString(), Money(summary.TotalValue) },
            Array.Empty<string>(),
            new[] { "按分类", "总数", "可用", "借出", "金额", "占比" }
        };
        rows.AddRange(summary.ByCategory.Select(x => new[]
        {
            $"{x.CategoryCode} {x.CategoryName}",
            x.Total.ToString(),
            x.Available.ToString(),
            x.Borrowed.ToString(),
            Money(x.TotalValue),
            $"{x.Percent:0.##}%"
        }));
        rows.Add(Array.Empty<string>());
        rows.Add(new[] { "按部门", "总数", "可用", "借出", "金额", "占比" });
        rows.AddRange(summary.ByDept.Select(x => new[]
        {
            $"{x.DepartmentCode} {x.DepartmentName}",
            x.Total.ToString(),
            x.Available.ToString(),
            x.Borrowed.ToString(),
            Money(x.TotalValue),
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
            new[] { "流程号", "资产编号", "资产名称", "分类", "借用人", "部门", "申请时间", "预计归还", "状态", "金额" }
        };
        var flows = await ApplyBorrowQuery(_db.ApprovalFlows.AsNoTracking(), query)
            .OrderByDescending(x => x.ApplyTime)
            .ToListAsync();
        rows.AddRange((await ToBorrowRows(flows)).Select(x => new[]
        {
            x.FlowNo,
            x.AssetNo,
            x.AssetName,
            $"{x.CategoryCode} {x.CategoryName}",
            x.Borrower,
            x.BorrowerDept ?? "",
            x.ApplyTime.ToString("yyyy-MM-dd HH:mm"),
            x.ReturnDate ?? "",
            x.Status,
            Money(x.Amount)
        }));
        return XlsxTable.Write(rows);
    }

    public async Task<List<OverdueReportRow>> QueryOverdueAsync()
    {
        var flows = await _db.ApprovalFlows.AsNoTracking()
            .Where(x => x.BizType == "borrow" && x.Status == "approved" && x.ReturnDate != null)
            .OrderByDescending(x => x.ApplyTime)
            .ToListAsync();
        var assetIds = flows.Select(x => x.AssetId).Distinct().ToArray();
        var borrowedAssets = await _db.Assets.AsNoTracking()
            .Where(x => assetIds.Contains(x.Id) && x.Status == AssetStatus.Borrowed)
            .ToDictionaryAsync(x => x.Id);
        var today = DateTime.UtcNow.Date;
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
            $"{x.CategoryCode} {x.CategoryName}",
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
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            ActionType = "remind",
            TargetType = "asset",
            TargetId = assetId.ToString(CultureInfo.InvariantCulture),
            Summary = $"逾期催办：{row.AssetNo} {row.AssetName}",
            Detail = $"借用人：{row.Borrower}；预计归还：{row.ReturnDate}；逾期：{row.OverdueDays}天",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<int> RemindOverdueBatchAsync(IReadOnlyCollection<int> assetIds, int? userId)
    {
        var ids = assetIds.Distinct().ToArray();
        foreach (var id in ids)
        {
            await RemindOverdueAsync(id, userId);
        }

        return ids.Length;
    }

    private IQueryable<ApprovalFlow> ApplyBorrowQuery(IQueryable<ApprovalFlow> queryable, BorrowReportQuery query)
    {
        queryable = queryable.Where(x => x.BizType == "borrow" && x.Status == "approved");
        if (query.StartTime.HasValue)
        {
            queryable = queryable.Where(x => x.ApplyTime >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            queryable = queryable.Where(x => x.ApplyTime <= query.EndTime.Value);
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
                "returned" => queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId && a.Status == AssetStatus.Available)),
                "borrowed" => queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId && a.Status == AssetStatus.Borrowed)),
                _ => queryable
            };
        }

        if (query.CategoryId.HasValue)
        {
            queryable = queryable.Where(x => _db.Assets.Any(a => a.Id == x.AssetId && a.CategoryId == query.CategoryId.Value));
        }

        return queryable;
    }

    private async Task<List<BorrowReportRow>> ToBorrowRows(IEnumerable<ApprovalFlow> flows)
    {
        var list = flows.ToList();
        var assetIds = list.Select(x => x.AssetId).Distinct().ToArray();
        var assets = await _db.Assets.AsNoTracking().Where(x => assetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var categoryIds = assets.Values.Select(x => x.CategoryId).Distinct().ToArray();
        var categories = await _db.AssetCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

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
                CategoryName = category?.Name ?? "",
                BorrowerId = x.ApplicantId,
                Borrower = x.Applicant,
                BorrowerDept = x.ApplicantDept,
                ReturnDate = x.ReturnDate,
                ApplyTime = x.ApplyTime,
                Status = asset?.Status == AssetStatus.Available ? "returned" : "borrowed",
                Amount = x.Amount
            };
        }).ToList();
    }

    private async Task<List<OverdueReportRow>> ToOverdueRows(List<(ApprovalFlow Flow, DateTime Due, int Days)> overdue)
    {
        var assetIds = overdue.Select(x => x.Flow.AssetId).Distinct().ToArray();
        var assets = await _db.Assets.AsNoTracking().Where(x => assetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var categoryIds = assets.Values.Select(x => x.CategoryId).Distinct().ToArray();
        var categories = await _db.AssetCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

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
                CategoryName = category?.Name ?? "",
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
            CategoryName = category.Name,
            Total = list.Count,
            Available = list.Count(x => x.Status == AssetStatus.Available),
            Borrowed = list.Count(x => x.Status == AssetStatus.Borrowed),
            TotalValue = list.Sum(AssetValue),
            Percent = Percent(list.Count, total)
        };
    }

    private static DeptStatRow ToDeptRow(Department department, IEnumerable<Asset> assets, int total)
    {
        var list = assets.ToList();
        return new DeptStatRow
        {
            DepartmentId = department.Id,
            DepartmentCode = department.Code,
            DepartmentName = department.Name,
            Total = list.Count,
            Available = list.Count(x => x.Status == AssetStatus.Available),
            Borrowed = list.Count(x => x.Status == AssetStatus.Borrowed),
            TotalValue = list.Sum(AssetValue),
            Percent = Percent(list.Count, total)
        };
    }

    private static Department RootDepartment(Department department, List<Department> departments)
    {
        var current = department;
        while (current.ParentId.HasValue)
        {
            var parent = departments.FirstOrDefault(x => x.Id == current.ParentId.Value);
            if (parent is null)
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    private static DateTime? ParseDate(string? text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, out var date) ? date.Date : null;

    private static decimal AssetValue(Asset asset)
        => asset.Price * asset.Quantity;

    private static decimal Percent(int count, int total)
        => total == 0 ? 0 : decimal.Round(count * 100m / total, 2);

    private static string Money(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
