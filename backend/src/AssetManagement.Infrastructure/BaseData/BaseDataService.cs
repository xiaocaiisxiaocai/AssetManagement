using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetManagement.Infrastructure.BaseData;

public class BaseDataService : IBaseDataService
{
    private const int MaxCategoryDepth = 3;

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    // 缓存键
    private const string CategoryTreeCacheKey = "category_tree";

    public BaseDataService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<DepartmentNodeDto>> GetDepartmentTreeAsync()
    {
        var departments = await _db.Departments
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .ToListAsync();
        var managerIds = departments.Where(x => x.ManagerId.HasValue).Select(x => x.ManagerId!.Value).Distinct().ToArray();
        var managers = await _db.Users
            .Where(x => managerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        return BuildDepartmentTree(null, departments, managers);
    }

    public async Task<DepartmentNodeDto> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        var department = new Department
        {
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Code = await NextDepartmentCodeAsync(),
            ManagerId = request.ManagerId,
            IsActive = true
        };
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        // 清除部门树缓存
        _cache.Remove("department_tree");

        return ToDepartmentDto(department, null);
    }

    public async Task<DepartmentNodeDto> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request)
    {
        var department = await _db.Departments.FindAsync(id)
            ?? throw new BizException(4045, "部门不存在");
        department.ParentId = request.ParentId;
        department.Name = request.Name.Trim();
        department.ManagerId = request.ManagerId;
        department.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        // 清除部门树缓存
        _cache.Remove("department_tree");

        return ToDepartmentDto(department, null);
    }

    public async Task DeleteDepartmentAsync(int id)
    {
        if (await _db.Departments.AnyAsync(x => x.ParentId == id))
        {
            throw new BizException(4090, "请先删除子部门");
        }

        var department = await _db.Departments.FindAsync(id)
            ?? throw new BizException(4045, "部门不存在");
        _db.Departments.Remove(department);
        await _db.SaveChangesAsync();

        // 清除部门树缓存
        _cache.Remove("department_tree");
    }

    public async Task<List<CategoryNodeDto>> GetCategoryTreeAsync()
    {
        // 从缓存获取分类树
        return await _cache.GetOrCreateAsync(CategoryTreeCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AppConstants.CategoryTreeCacheMinutes);
            var categories = await _db.AssetCategories
                .OrderBy(x => x.Code)
                .ThenBy(x => x.Id)
                .ToListAsync();
            return BuildCategoryTree(null, categories);
        }) ?? new List<CategoryNodeDto>();
    }

    public async Task<CategoryNodeDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var all = await _db.AssetCategories.ToListAsync();
        var parent = FindCategory(request.ParentId, all);
        var newDepth = parent is null ? 1 : CategoryDepth(parent, all) + 1;
        EnsureCategoryMaxDepth(newDepth);

        var category = new AssetCategory
        {
            ParentId = request.ParentId,
            CodeSeg = request.CodeSeg.Trim(),
            Code = CategoryCodeService.Compose(parent?.Code, request.CodeSeg),
            Remark = CategoryRemark(request.ParentId, request.Remark)
        };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        // 清除分类树缓存
        _cache.Remove(CategoryTreeCacheKey);

        return ToCategoryDto(category);
    }

    public async Task<CategoryNodeDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
    {
        var all = await _db.AssetCategories.ToListAsync();
        var category = all.SingleOrDefault(x => x.Id == id)
            ?? throw new BizException(4046, "资产分类不存在");
        var parent = FindCategory(request.ParentId, all);
        if (request.ParentId == id || DescendantCategoryIds(id, all).Contains(request.ParentId ?? 0))
        {
            throw new BizException(4095, "不能将分类移动到自身或子分类下");
        }

        var targetDepth = parent is null ? 1 : CategoryDepth(parent, all) + 1;
        var subtreeDepth = CategorySubtreeDepth(id, all);
        EnsureCategoryMaxDepth(targetDepth + subtreeDepth - 1);

        category.ParentId = request.ParentId;
        category.CodeSeg = request.CodeSeg.Trim();
        category.Remark = CategoryRemark(request.ParentId, request.Remark);

        var subtree = BuildCategoryEntityTree(category, all);
        CategoryCodeService.Recalc(subtree, parent?.Code);
        await _db.SaveChangesAsync();

        // 清除分类树缓存
        _cache.Remove(CategoryTreeCacheKey);

        return ToCategoryDto(category);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var all = await _db.AssetCategories.ToListAsync();
        var root = all.SingleOrDefault(x => x.Id == id)
            ?? throw new BizException(4046, "资产分类不存在");
        var ids = DescendantCategoryIds(id, all).Append(id).ToArray();
        _db.AssetCategories.RemoveRange(all.Where(x => ids.Contains(x.Id)));
        await _db.SaveChangesAsync();

        // 清除分类树缓存
        _cache.Remove(CategoryTreeCacheKey);
    }

    public async Task<List<LocationNodeDto>> GetLocationTreeAsync()
        => await _db.Locations
            .OrderBy(x => x.Id)
            .Select(x => ToLocationDto(x))
            .ToListAsync();

    public async Task<LocationNodeDto> CreateLocationAsync(CreateLocationRequest request)
    {
        var location = new Location
        {
            Name = request.Name.Trim()
        };
        _db.Locations.Add(location);
        await _db.SaveChangesAsync();
        return ToLocationDto(location);
    }

    public async Task<LocationNodeDto> UpdateLocationAsync(int id, UpdateLocationRequest request)
    {
        var location = await _db.Locations.FindAsync(id)
            ?? throw new BizException(4047, "位置不存在");
        location.Name = request.Name.Trim();
        await _db.SaveChangesAsync();
        return ToLocationDto(location);
    }

    public async Task DeleteLocationAsync(int id)
    {
        var location = await _db.Locations.FindAsync(id)
            ?? throw new BizException(4047, "位置不存在");
        _db.Locations.Remove(location);
        await _db.SaveChangesAsync();
    }

    public async Task<List<SystemSettingDto>> GetSettingsAsync()
        => await _db.SystemSettings
            .OrderBy(x => x.Key)
            .Select(x => ToSettingDto(x))
            .ToListAsync();

    public async Task<List<SystemSettingDto>> SaveSettingsAsync(IEnumerable<SaveSystemSettingRequest> requests)
    {
        foreach (var request in requests)
        {
            var key = request.Key.Trim();
            var setting = await _db.SystemSettings.SingleOrDefaultAsync(x => x.Key == key);
            if (setting is null)
            {
                setting = new SystemSetting { Key = key };
                _db.SystemSettings.Add(setting);
            }

            setting.Value = request.Value;
            setting.Description = request.Description;
        }

        await _db.SaveChangesAsync();
        return await GetSettingsAsync();
    }

    private static List<DepartmentNodeDto> BuildDepartmentTree(int? parentId, List<Department> departments, Dictionary<int, string> managers)
        => departments
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToDepartmentDto(x, x.ManagerId.HasValue && managers.TryGetValue(x.ManagerId.Value, out var name) ? name : null);
                return dto with { Children = BuildDepartmentTree(x.Id, departments, managers) };
            })
            .ToList();

    private static List<CategoryNodeDto> BuildCategoryTree(int? parentId, List<AssetCategory> categories)
        => categories
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToCategoryDto(x);
                return dto with { Children = BuildCategoryTree(x.Id, categories) };
            })
            .ToList();

    private static AssetCategory BuildCategoryEntityTree(AssetCategory node, List<AssetCategory> all)
    {
        node.Children = all.Where(x => x.ParentId == node.Id).ToList();
        foreach (var child in node.Children)
        {
            BuildCategoryEntityTree(child, all);
        }

        return node;
    }

    private static IEnumerable<int> DescendantCategoryIds(int parentId, List<AssetCategory> all)
    {
        foreach (var child in all.Where(x => x.ParentId == parentId))
        {
            yield return child.Id;
            foreach (var id in DescendantCategoryIds(child.Id, all))
            {
                yield return id;
            }
        }
    }

    private static AssetCategory? FindCategory(int? id, List<AssetCategory> all)
    {
        if (!id.HasValue)
        {
            return null;
        }

        return all.SingleOrDefault(x => x.Id == id.Value)
            ?? throw new BizException(4046, "资产分类不存在");
    }

    private static int CategoryDepth(AssetCategory category, List<AssetCategory> all)
    {
        var depth = 1;
        var parentId = category.ParentId;
        while (parentId.HasValue)
        {
            var parent = all.SingleOrDefault(x => x.Id == parentId.Value)
                ?? throw new BizException(4046, "资产分类不存在");
            depth++;
            parentId = parent.ParentId;
        }

        return depth;
    }

    private static int CategorySubtreeDepth(int categoryId, List<AssetCategory> all)
    {
        var childDepths = all
            .Where(x => x.ParentId == categoryId)
            .Select(x => CategorySubtreeDepth(x.Id, all))
            .ToList();
        return childDepths.Count == 0 ? 1 : childDepths.Max() + 1;
    }

    private static void EnsureCategoryMaxDepth(int depth)
    {
        if (depth > MaxCategoryDepth)
        {
            throw new BizException(4096, "资产分类最多维护三级");
        }
    }

    private static DepartmentNodeDto ToDepartmentDto(Department x, string? managerName) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        ManagerName = managerName,
        AssetCount = 0,
        IsActive = x.IsActive
    };

    private static CategoryNodeDto ToCategoryDto(AssetCategory x) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        CodeSeg = x.CodeSeg,
        Code = x.Code,
        Remark = x.ParentId.HasValue ? x.Remark : null
    };

    private static string? CategoryRemark(int? parentId, string? remark)
        => parentId.HasValue ? EmptyToNull(remark) : null;

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static LocationNodeDto ToLocationDto(Location x) => new()
    {
        Id = x.Id,
        Name = x.Name
    };

    private async Task<string> NextDepartmentCodeAsync()
    {
        var next = await _db.Departments.AnyAsync()
            ? await _db.Departments.MaxAsync(x => x.Id) + 1
            : 1;
        string code;
        do
        {
            code = $"D{next:0000}";
            next++;
        } while (await _db.Departments.AnyAsync(x => x.Code == code));

        return code;
    }

    private static SystemSettingDto ToSettingDto(SystemSetting x) => new()
    {
        Id = x.Id,
        Key = x.Key,
        Value = x.Value,
        Description = x.Description
    };
}
