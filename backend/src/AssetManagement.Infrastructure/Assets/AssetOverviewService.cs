using AssetManagement.Application.Assets;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Assets;

public class AssetOverviewService : IAssetOverviewService
{
    private readonly AppDbContext _db;

    public AssetOverviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AssetOverviewDto> GetOverviewAsync()
    {
        var categories = await _db.AssetCategories
            .AsNoTracking()
            .Select(category => new CategoryInfo
            {
                Id = category.Id,
                ParentId = category.ParentId,
                Code = category.Code,
                CodeSeg = category.CodeSeg,
                Remark = category.Remark,
                IsDeleted = category.IsDeleted
            })
            .ToListAsync();

        var categoryMap = categories.ToDictionary(category => category.Id);
        var categoryStats = categories
            .Where(category => IsActiveSecondLevel(category, categoryMap))
            .ToDictionary(
                category => category.Id,
                category => new MutableCategoryStats(
                    category,
                    categoryMap[category.ParentId!.Value].Code));

        // 数据库仅返回分类、状态和记录数，不加载完整资产实体，也不使用 Quantity 汇总。
        var assetGroups = await _db.Assets
            .AsNoTracking()
            .Where(asset => !asset.IsDeleted)
            .GroupBy(asset => new { asset.CategoryId, asset.Status })
            .Select(group => new
            {
                group.Key.CategoryId,
                group.Key.Status,
                Count = group.Count()
            })
            .ToListAsync();

        var total = 0;
        var available = 0;
        var borrowed = 0;
        var uncategorizedTotal = 0;

        foreach (var group in assetGroups)
        {
            total += group.Count;
            if (group.Status == AssetStatus.Available)
            {
                available += group.Count;
            }
            else if (group.Status == AssetStatus.Borrowed)
            {
                borrowed += group.Count;
            }

            var secondLevelId = ResolveSecondLevelCategoryId(group.CategoryId, categoryMap);
            if (!secondLevelId.HasValue || !categoryStats.TryGetValue(secondLevelId.Value, out var stats))
            {
                uncategorizedTotal += group.Count;
                continue;
            }

            stats.Total += group.Count;
            if (group.Status == AssetStatus.Available)
            {
                stats.Available += group.Count;
            }
            else if (group.Status == AssetStatus.Borrowed)
            {
                stats.Borrowed += group.Count;
            }
        }

        return new AssetOverviewDto
        {
            Total = total,
            Available = available,
            Borrowed = borrowed,
            UncategorizedTotal = uncategorizedTotal,
            Categories = categoryStats.Values
                .OrderBy(stats => stats.ParentCode, StringComparer.Ordinal)
                .ThenBy(stats => stats.Category.Code, StringComparer.Ordinal)
                .ThenBy(stats => stats.Category.Id)
                .Select(stats => new AssetOverviewCategoryDto
                {
                    CategoryId = stats.Category.Id,
                    CategoryCode = stats.Category.Code,
                    Label = stats.Category.CodeSeg,
                    ParentCode = stats.ParentCode,
                    Remark = stats.Category.Remark,
                    Total = stats.Total,
                    Available = stats.Available,
                    Borrowed = stats.Borrowed
                })
                .ToList()
        };
    }

    private static bool IsActiveSecondLevel(
        CategoryInfo category,
        IReadOnlyDictionary<int, CategoryInfo> categoryMap)
        => !category.IsDeleted
           && category.ParentId is int parentId
           && categoryMap.TryGetValue(parentId, out var parent)
           && !parent.IsDeleted
           && parent.ParentId is null;

    private static int? ResolveSecondLevelCategoryId(
        int categoryId,
        IReadOnlyDictionary<int, CategoryInfo> categoryMap)
    {
        var visited = new HashSet<int>();
        var currentId = categoryId;

        while (visited.Add(currentId)
               && categoryMap.TryGetValue(currentId, out var current)
               && !current.IsDeleted)
        {
            if (current.ParentId is not int parentId
                || !categoryMap.TryGetValue(parentId, out var parent)
                || parent.IsDeleted)
            {
                return null;
            }

            if (parent.ParentId is null)
            {
                return current.Id;
            }

            currentId = parent.Id;
        }

        return null;
    }

    private sealed class CategoryInfo
    {
        public int Id { get; init; }
        public int? ParentId { get; init; }
        public string Code { get; init; } = "";
        public string CodeSeg { get; init; } = "";
        public string? Remark { get; init; }
        public bool IsDeleted { get; init; }
    }

    private sealed class MutableCategoryStats
    {
        public MutableCategoryStats(CategoryInfo category, string parentCode)
        {
            Category = category;
            ParentCode = parentCode;
        }

        public CategoryInfo Category { get; }
        public string ParentCode { get; }
        public int Total { get; set; }
        public int Available { get; set; }
        public int Borrowed { get; set; }
    }
}
