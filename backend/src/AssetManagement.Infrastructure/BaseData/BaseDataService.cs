using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Persistence;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace AssetManagement.Infrastructure.BaseData;

public class BaseDataService : IBaseDataService
{
    private const int MaxCategoryDepth = 3;
    private const int MaxCategoryCodeSegLength = 20;
    private const int MaxRetentionDays = 3650;
    private const int MaxRetentionMonths = 120;
    private const string DefaultCategoryCodeLength = "2-6";
    private const string DefaultCategoryCodeRegex = "^[A-Za-z0-9]+$";

    private static readonly HashSet<string> BooleanSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "audit_cleanup_enabled",
        "database_backup_enabled",
        "material.transfer.approval.enabled"
    };

    private static readonly HashSet<string> TimeSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "audit_cleanup_time",
        "database_backup_time"
    };

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
        ValidateDepartmentRequest(request.Name);
        var name = request.Name.Trim();
        await EnsureDepartmentNameAvailableAsync(name);
        await EnsureDepartmentManagerAvailableAsync(request.ManagerId);
        var department = new Department
        {
            ParentId = request.ParentId,
            Name = name,
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
        ValidateDepartmentRequest(request.Name);
        var department = await _db.Departments.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4045, "部门不存在");
        var name = request.Name.Trim();
        await EnsureDepartmentNameAvailableAsync(name, id);
        if (request.IsActive)
        {
            await EnsureDepartmentManagerAvailableAsync(request.ManagerId, id);
        }
        department.ParentId = request.ParentId;
        department.Name = name;
        department.ManagerId = request.ManagerId;
        department.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        // 清除部门树缓存
        _cache.Remove("department_tree");

        return ToDepartmentDto(department, null);
    }

    private static void ValidateDepartmentRequest(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BizException(4001, "部门名称不能为空");
        }
    }

    private async Task EnsureDepartmentNameAvailableAsync(string name, int? selfId = null)
    {
        if (await _db.Departments.AnyAsync(x => x.Name == name && x.Id != selfId))
        {
            throw new BizException(4094, "部门名称已存在");
        }
    }

    private async Task EnsureDepartmentManagerAvailableAsync(int? managerId, int? selfId = null)
    {
        if (managerId is null)
        {
            return;
        }

        if (await _db.Departments.AnyAsync(x => x.ManagerId == managerId && x.IsActive && x.Id != selfId))
        {
            throw new BizException(4094, "负责人已负责其他部门");
        }
    }

    public async Task DeleteDepartmentAsync(int id)
    {
        if (await _db.Departments.AnyAsync(x => x.ParentId == id))
        {
            throw new BizException(4090, "请先删除子部门");
        }
        if (await _db.Users.AnyAsync(x => x.DepartmentId == id))
        {
            throw new BizException(4094, "部门已被用户使用，不能删除");
        }
        if (await _db.Assets.AnyAsync(x => x.DepartmentId == id))
        {
            throw new BizException(4094, "部门已被资产使用，不能删除");
        }
        if (await _db.TestMaterials.AnyAsync(x => x.DepartmentId == id))
        {
            throw new BizException(4094, "部门已被测试料件使用，不能删除");
        }

        var department = await _db.Departments.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4045, "部门不存在");
        _db.Departments.Remove(department);
        await _db.SaveChangesAsync();

        // 清除部门树缓存
        _cache.Remove("department_tree");
    }

    public async Task<List<CategoryNodeDto>> GetCategoryTreeAsync(string? deleteStatus = null)
    {
        // 删除状态:all=全部(含已删除),deleted=仅已删除,其余=仅未删除
        var status = (deleteStatus?.Trim().ToLowerInvariant()) switch
        {
            "all" => "all",
            "deleted" => "deleted",
            _ => "active",
        };
        // 从缓存获取分类树
        return await _cache.GetOrCreateAsync($"{CategoryTreeCacheKey}:{status}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AppConstants.CategoryTreeCacheMinutes);
            var queryable = _db.AssetCategories.AsQueryable();
            queryable = status switch
            {
                "all" => queryable,
                "deleted" => queryable.Where(x => x.IsDeleted),
                _ => queryable.Where(x => !x.IsDeleted),
            };
            var categories = await queryable
                .OrderBy(x => x.Code)
                .ThenBy(x => x.Id)
                .ToListAsync();
            return BuildCategoryTreeRoots(categories);
        }) ?? new List<CategoryNodeDto>();
    }

    public async Task<CategoryNodeDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var all = await _db.AssetCategories.Where(x => !x.IsDeleted).ToListAsync();
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
        await ValidateCategoryCodeSegAsync(newDepth, category.CodeSeg);
        await EnsureCategoryCodeAvailableAsync(category.Code);
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        // 清除分类树缓存
        ClearCategoryTreeCache();

        return ToCategoryDto(category);
    }

    public async Task<CategoryNodeDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
    {
        var all = await _db.AssetCategories.Where(x => !x.IsDeleted).AsTracking().ToListAsync();
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
        await ValidateCategorySubtreeCodeSegsAsync(category, all, targetDepth);

        var subtree = BuildCategoryEntityTree(category, all);
        CategoryCodeService.Recalc(subtree, parent?.Code);
        await EnsureCategoryCodesAvailableAsync(subtree);
        await _db.SaveChangesAsync();

        // 清除分类树缓存
        ClearCategoryTreeCache();

        return ToCategoryDto(category);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var all = await _db.AssetCategories.AsTracking().ToListAsync();
        var root = all.SingleOrDefault(x => x.Id == id)
            ?? throw new BizException(4046, "资产分类不存在");
        if (root.IsDeleted)
        {
            return;
        }
        var ids = DescendantCategoryIds(id, all).Append(id).ToArray();
        if (await _db.Assets.AnyAsync(x => ids.Contains(x.CategoryId)))
        {
            throw new BizException(4098, "该分类下存在资产，不能删除");
        }

        var now = DateTime.UtcNow;
        foreach (var category in all.Where(x => ids.Contains(x.Id)))
        {
            category.IsDeleted = true;
            category.DeletedAt = now;
        }
        await _db.SaveChangesAsync();

        // 清除分类树缓存
        ClearCategoryTreeCache();
    }

    public async Task PurgeCategoryAsync(int id)
    {
        var all = await _db.AssetCategories.AsTracking().ToListAsync();
        var root = all.SingleOrDefault(x => x.Id == id)
            ?? throw new BizException(4046, "资产分类不存在");
        var ids = DescendantCategoryIds(id, all).Append(id).ToArray();
        var subtree = all.Where(x => ids.Contains(x.Id)).ToList();
        if (subtree.Any(x => !x.IsDeleted))
        {
            throw new BizException(4097, "请先删除分类后再彻底删除");
        }
        if (await _db.Assets.AnyAsync(x => ids.Contains(x.CategoryId)))
        {
            throw new BizException(4098, "该分类下存在资产，不能彻底删除");
        }

        _db.AssetCategories.RemoveRange(subtree);
        await _db.SaveChangesAsync();
        ClearCategoryTreeCache();
    }

    public async Task RestoreCategoryAsync(int id)
    {
        var all = await _db.AssetCategories.AsTracking().ToListAsync();
        var root = all.SingleOrDefault(x => x.Id == id)
            ?? throw new BizException(4046, "资产分类不存在");
        if (!root.IsDeleted)
        {
            throw new BizException(4099, "分类未删除，无需恢复");
        }
        // 上级若仍处于删除状态,需先恢复上级,避免出现"孤儿"节点
        if (root.ParentId.HasValue)
        {
            var parent = all.SingleOrDefault(x => x.Id == root.ParentId.Value);
            if (parent is not null && parent.IsDeleted)
            {
                throw new BizException(4096, "请先恢复上级分类");
            }
        }
        // 与删除对称:级联恢复该分类及其所有子孙
        var ids = DescendantCategoryIds(id, all).Append(id).ToArray();
        foreach (var category in all.Where(x => ids.Contains(x.Id) && x.IsDeleted))
        {
            category.IsDeleted = false;
            category.DeletedAt = null;
        }
        await _db.SaveChangesAsync();
        ClearCategoryTreeCache();
    }

    public async Task<List<LocationNodeDto>> GetLocationTreeAsync()
        => await _db.Locations
            .OrderBy(x => x.Id)
            .Select(x => ToLocationDto(x))
            .ToListAsync();

    public async Task<LocationNodeDto> CreateLocationAsync(CreateLocationRequest request)
    {
        var name = request.Name.Trim();
        await EnsureLocationNameAvailableAsync(name);
        var location = new Location
        {
            Name = name
        };
        _db.Locations.Add(location);
        await _db.SaveChangesAsync();
        return ToLocationDto(location);
    }

    public async Task<LocationNodeDto> UpdateLocationAsync(int id, UpdateLocationRequest request)
    {
        var location = await _db.Locations.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4047, "位置不存在");
        var name = request.Name.Trim();
        await EnsureLocationNameAvailableAsync(name, id);
        location.Name = name;
        await _db.SaveChangesAsync();
        return ToLocationDto(location);
    }

    public async Task DeleteLocationAsync(int id)
    {
        var location = await _db.Locations.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4047, "位置不存在");
        if (await _db.Assets.AnyAsync(x => x.LocationId == id))
        {
            throw new BizException(4094, "位置已被资产使用，不能删除");
        }
        if (await _db.TestMaterials.AnyAsync(x => x.LocationId == id))
        {
            throw new BizException(4094, "位置已被测试料件使用，不能删除");
        }
        _db.Locations.Remove(location);
        await _db.SaveChangesAsync();
    }

    public async Task<List<SystemSettingDto>> GetSettingsAsync()
        => await _db.SystemSettings
            .OrderBy(x => x.Key)
            .Select(x => ToSettingDto(x))
            .ToListAsync();

    public async Task<RuntimeSettingsDto> GetRuntimeSettingsAsync()
    {
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .Where(x =>
                x.Key == "page_size"
                || x.Key == "attachment_max_mb"
                || x.Key.StartsWith("category_code_level"))
            .ToDictionaryAsync(x => x.Key, x => x.Value);

        return new RuntimeSettingsDto
        {
            PageSize = ReadIntSetting(settings, "page_size", 20, 1, AppConstants.MaxPageSize),
            AttachmentMaxMb = ReadIntSetting(settings, "attachment_max_mb", 5, 1, 100),
            CategoryCodeRules = BuildCategoryCodeRules(settings)
        };
    }

    public async Task<List<SystemSettingDto>> SaveSettingsAsync(IEnumerable<SaveSystemSettingRequest> requests)
    {
        var existingKeys = await _db.SystemSettings
            .AsNoTracking()
            .Select(x => x.Key)
            .ToListAsync();
        var existingKeySet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var request in requests)
        {
            var key = request.Key.Trim();
            if (!existingKeySet.Contains(key))
            {
                throw new BizException(4001, $"系统参数「{key}」不存在，不能新增");
            }

            var setting = await _db.SystemSettings.AsTracking().SingleOrDefaultAsync(x => x.Key == key)
                ?? throw new BizException(4001, $"系统参数「{key}」不存在，不能新增");
            setting.Value = NormalizeSettingValue(key, request.Value);
        }

        await _db.SaveChangesAsync();
        return await GetSettingsAsync();
    }

    private static string NormalizeSettingValue(string key, string value)
    {
        var raw = value?.Trim() ?? string.Empty;

        if (BooleanSettingKeys.Contains(key))
        {
            if (!bool.TryParse(raw, out var boolValue))
            {
                throw new BizException(4001, $"系统参数「{key}」必须是布尔值 true 或 false");
            }

            return boolValue ? "true" : "false";
        }

        if (TimeSettingKeys.Contains(key))
        {
            if (!TimeSpan.TryParseExact(raw, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
            {
                throw new BizException(4001, $"系统参数「{key}」必须是 HH:mm 格式的时间");
            }

            return time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        return key switch
        {
            "attachment_max_mb" => NormalizeIntSetting(key, raw, 1, 100),
            "audit_retention_months" => NormalizeIntSetting(key, raw, 1, MaxRetentionMonths),
            "database_backup_retention_days" => NormalizeIntSetting(key, raw, 1, MaxRetentionDays),
            "page_size" => NormalizeIntSetting(key, raw, 1, AppConstants.MaxPageSize),
            "category_code_level1_length" => NormalizeLengthRuleSetting(key, raw),
            "category_code_level2_length" => NormalizeLengthRuleSetting(key, raw),
            "category_code_level3_length" => NormalizeLengthRuleSetting(key, raw),
            "category_code_level1_regex" => NormalizeRegexSetting(key, raw),
            "category_code_level2_regex" => NormalizeRegexSetting(key, raw),
            "category_code_level3_regex" => NormalizeRegexSetting(key, raw),
            "audit_retention_days" => NormalizeAuditRetentionDays(raw),
            "database_backup_path" => NormalizeRequiredTextSetting(key, raw),
            _ => raw
        };
    }

    private static string NormalizeAuditRetentionDays(string raw)
    {
        if (!int.TryParse(raw, out var value) || value is not (7 or 14 or 30))
        {
            throw new BizException(4001, "系统参数「audit_retention_days」必须是 7/14/30");
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeIntSetting(string key, string raw, int min, int max)
    {
        if (!int.TryParse(raw, out var value) || value < min || value > max)
        {
            throw new BizException(4001, $"系统参数「{key}」必须是 {min}-{max} 的整数");
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeRequiredTextSetting(string key, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new BizException(4001, $"系统参数「{key}」不能为空");
        }

        return raw;
    }

    private static string NormalizeLengthRuleSetting(string key, string raw)
    {
        var parsed = ParseLengthRule(raw);
        if (parsed is null)
        {
            throw new BizException(4001, $"系统参数「{key}」必须是 1-20 的整数或长度范围");
        }

        return parsed.Value.Min == parsed.Value.Max
            ? parsed.Value.Min.ToString(CultureInfo.InvariantCulture)
            : $"{parsed.Value.Min}-{parsed.Value.Max}";
    }

    private static string NormalizeRegexSetting(string key, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new BizException(4001, $"系统参数「{key}」不能为空");
        }

        try
        {
            _ = new Regex(raw, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        }
        catch (ArgumentException)
        {
            throw new BizException(4001, $"系统参数「{key}」必须是合法正则表达式");
        }

        return raw;
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

    private static List<CategoryNodeDto> BuildCategoryTreeRoots(List<AssetCategory> categories)
    {
        var ids = categories.Select(x => x.Id).ToHashSet();
        return categories
            .Where(x => !x.ParentId.HasValue || !ids.Contains(x.ParentId.Value))
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToCategoryDto(x);
                return dto with { Children = BuildCategoryTree(x.Id, categories) };
            })
            .ToList();
    }

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

    private async Task ValidateCategoryCodeSegAsync(int level, string codeSeg)
    {
        var rules = await LoadCategoryCodeRulesAsync();
        var rule = GetCategoryCodeRule(rules, level);
        var levelName = CategoryLevelName(level);
        var lengthRule = ParseLengthRule(rule.Length)!.Value;
        if (codeSeg.Length < lengthRule.Min || codeSeg.Length > lengthRule.Max)
        {
            throw new BizException(4001, $"{levelName}分类编码段必须是 {FormatLengthRule(lengthRule)} 位");
        }

        if (!Regex.IsMatch(codeSeg, rule.Regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
        {
            throw new BizException(4001, $"{levelName}分类编码段格式不正确，应匹配 {rule.Regex}");
        }
    }

    private async Task ValidateCategorySubtreeCodeSegsAsync(AssetCategory root, List<AssetCategory> all, int targetDepth)
    {
        var rules = await LoadCategoryCodeRulesAsync();
        foreach (var item in FlattenCategoryWithDepth(root, all, targetDepth))
        {
            var rule = GetCategoryCodeRule(rules, item.Depth);
            var levelName = CategoryLevelName(item.Depth);
            var lengthRule = ParseLengthRule(rule.Length)!.Value;
            if (item.Category.CodeSeg.Length < lengthRule.Min || item.Category.CodeSeg.Length > lengthRule.Max)
            {
                throw new BizException(4001, $"{levelName}分类编码段必须是 {FormatLengthRule(lengthRule)} 位");
            }

            if (!Regex.IsMatch(item.Category.CodeSeg, rule.Regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
            {
                throw new BizException(4001, $"{levelName}分类编码段格式不正确，应匹配 {rule.Regex}");
            }
        }
    }

    private async Task<CategoryCodeRulesDto> LoadCategoryCodeRulesAsync()
    {
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key.StartsWith("category_code_level"))
            .ToDictionaryAsync(x => x.Key, x => x.Value);
        return BuildCategoryCodeRules(settings);
    }

    private static CategoryCodeRulesDto BuildCategoryCodeRules(IReadOnlyDictionary<string, string> settings)
        => new()
        {
            Level1 = BuildCategoryCodeRule(settings, 1),
            Level2 = BuildCategoryCodeRule(settings, 2),
            Level3 = BuildCategoryCodeRule(settings, 3)
        };

    private static CategoryCodeRuleDto BuildCategoryCodeRule(IReadOnlyDictionary<string, string> settings, int level)
    {
        var length = settings.TryGetValue($"category_code_level{level}_length", out var rawLength)
            ? NormalizeLengthRuleSetting($"category_code_level{level}_length", rawLength)
            : DefaultCategoryCodeLength;

        return new CategoryCodeRuleDto
        {
            Length = length,
            Regex = settings.TryGetValue($"category_code_level{level}_regex", out var regex) && !string.IsNullOrWhiteSpace(regex)
                ? regex
                : DefaultCategoryCodeRegex
        };
    }

    private static (int Min, int Max)? ParseLengthRule(string raw)
    {
        var text = raw.Trim();
        if (int.TryParse(text, out var exact))
        {
            return exact is >= 1 and <= MaxCategoryCodeSegLength ? (exact, exact) : null;
        }

        var parts = text.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var min)
            || !int.TryParse(parts[1], out var max)
            || min < 1
            || max > MaxCategoryCodeSegLength
            || min > max)
        {
            return null;
        }

        return (min, max);
    }

    private static string FormatLengthRule((int Min, int Max) rule)
        => rule.Min == rule.Max
            ? rule.Min.ToString(CultureInfo.InvariantCulture)
            : $"{rule.Min}-{rule.Max}";

    private static CategoryCodeRuleDto GetCategoryCodeRule(CategoryCodeRulesDto rules, int level)
        => level switch
        {
            1 => rules.Level1,
            2 => rules.Level2,
            3 => rules.Level3,
            _ => throw new BizException(4096, "资产分类最多维护三级")
        };

    private static string CategoryLevelName(int level)
        => level switch
        {
            1 => "一级",
            2 => "二级",
            3 => "三级",
            _ => $"{level}级"
        };

    private static IEnumerable<(AssetCategory Category, int Depth)> FlattenCategoryWithDepth(
        AssetCategory root,
        List<AssetCategory> all,
        int depth)
    {
        yield return (root, depth);
        foreach (var child in all.Where(x => x.ParentId == root.Id))
        {
            foreach (var item in FlattenCategoryWithDepth(child, all, depth + 1))
            {
                yield return item;
            }
        }
    }

    private async Task EnsureCategoryCodeAvailableAsync(string code, int? selfId = null)
    {
        if (await _db.AssetCategories.AnyAsync(x => x.Code == code && x.Id != selfId))
        {
            throw new BizException(4094, "已存在对应编码段");
        }
    }

    private async Task EnsureCategoryCodesAvailableAsync(AssetCategory subtree)
    {
        var subtreeItems = FlattenCategorySubtree(subtree).ToList();
        var subtreeIds = subtreeItems.Select(x => x.Id).ToArray();
        var subtreeCodes = subtreeItems.Select(x => x.Code).Distinct().ToArray();
        if (await _db.AssetCategories.AnyAsync(x => subtreeCodes.Contains(x.Code) && !subtreeIds.Contains(x.Id)))
        {
            throw new BizException(4094, "已存在对应编码段");
        }
        if (subtreeItems.GroupBy(x => x.Code).Any(x => x.Count() > 1))
        {
            throw new BizException(4094, "已存在对应编码段");
        }
    }

    private static IEnumerable<AssetCategory> FlattenCategorySubtree(AssetCategory node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var item in FlattenCategorySubtree(child))
            {
                yield return item;
            }
        }
    }

    private async Task EnsureLocationNameAvailableAsync(string name, int? selfId = null)
    {
        if (await _db.Locations.AnyAsync(x => x.Name == name && x.Id != selfId))
        {
            throw new BizException(4094, "存放位置已存在");
        }
    }

    private static DepartmentNodeDto ToDepartmentDto(Department x, string? managerName) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        ManagerId = x.ManagerId,
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
        Remark = x.ParentId.HasValue ? x.Remark : null,
        IsDeleted = x.IsDeleted,
        DeletedAt = x.DeletedAt
    };

    private void ClearCategoryTreeCache()
    {
        _cache.Remove($"{CategoryTreeCacheKey}:active");
        _cache.Remove($"{CategoryTreeCacheKey}:all");
        _cache.Remove($"{CategoryTreeCacheKey}:deleted");
    }

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

    private static int ReadIntSetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int fallback,
        int min,
        int max)
    {
        if (!settings.TryGetValue(key, out var raw) || !int.TryParse(raw, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
