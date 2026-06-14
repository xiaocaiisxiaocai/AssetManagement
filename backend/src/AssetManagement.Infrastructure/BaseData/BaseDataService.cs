using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.BaseData;

public class BaseDataService : IBaseDataService
{
    private readonly AppDbContext _db;

    public BaseDataService(AppDbContext db)
    {
        _db = db;
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
            Code = request.Code.Trim(),
            ManagerId = request.ManagerId,
            IsActive = true
        };
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();
        return ToDepartmentDto(department, null);
    }

    public async Task<DepartmentNodeDto> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request)
    {
        var department = await _db.Departments.FindAsync(id)
            ?? throw new BizException(4045, "部门不存在");
        department.ParentId = request.ParentId;
        department.Name = request.Name.Trim();
        department.Code = request.Code.Trim();
        department.ManagerId = request.ManagerId;
        department.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
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
    }

    public async Task<List<CategoryNodeDto>> GetCategoryTreeAsync()
    {
        var categories = await _db.AssetCategories
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .ToListAsync();
        return BuildCategoryTree(null, categories);
    }

    public async Task<CategoryNodeDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var parentCode = request.ParentId.HasValue
            ? (await _db.AssetCategories.FindAsync(request.ParentId.Value)
                ?? throw new BizException(4046, "资产分类不存在")).Code
            : null;

        var category = new AssetCategory
        {
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            CodeSeg = request.CodeSeg.Trim(),
            Code = CategoryCodeService.Compose(parentCode, request.CodeSeg)
        };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        return ToCategoryDto(category);
    }

    public async Task<CategoryNodeDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
    {
        var all = await _db.AssetCategories.ToListAsync();
        var category = all.SingleOrDefault(x => x.Id == id)
            ?? throw new BizException(4046, "资产分类不存在");
        category.ParentId = request.ParentId;
        category.Name = request.Name.Trim();
        category.CodeSeg = request.CodeSeg.Trim();

        var parentCode = request.ParentId.HasValue
            ? all.Single(x => x.Id == request.ParentId.Value).Code
            : null;
        var subtree = BuildCategoryEntityTree(category, all);
        CategoryCodeService.Recalc(subtree, parentCode);
        await _db.SaveChangesAsync();
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
    }

    public async Task<List<LocationNodeDto>> GetLocationTreeAsync()
    {
        var locations = await _db.Locations
            .OrderBy(x => x.Id)
            .ToListAsync();
        return BuildLocationTree(null, locations);
    }

    public async Task<LocationNodeDto> CreateLocationAsync(CreateLocationRequest request)
    {
        var location = new Location
        {
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            QrCode = request.QrCode
        };
        _db.Locations.Add(location);
        await _db.SaveChangesAsync();
        return ToLocationDto(location);
    }

    public async Task<LocationNodeDto> UpdateLocationAsync(int id, UpdateLocationRequest request)
    {
        var location = await _db.Locations.FindAsync(id)
            ?? throw new BizException(4047, "位置不存在");
        location.ParentId = request.ParentId;
        location.Name = request.Name.Trim();
        location.QrCode = request.QrCode;
        await _db.SaveChangesAsync();
        return ToLocationDto(location);
    }

    public async Task DeleteLocationAsync(int id)
    {
        if (await _db.Locations.AnyAsync(x => x.ParentId == id))
        {
            throw new BizException(4091, "请先删除子位置");
        }

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

    private static List<LocationNodeDto> BuildLocationTree(int? parentId, List<Location> locations)
        => locations
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToLocationDto(x);
                return dto with { Children = BuildLocationTree(x.Id, locations) };
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

    private static DepartmentNodeDto ToDepartmentDto(Department x, string? managerName) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        Code = x.Code,
        ManagerName = managerName,
        AssetCount = 0,
        IsActive = x.IsActive
    };

    private static CategoryNodeDto ToCategoryDto(AssetCategory x) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        CodeSeg = x.CodeSeg,
        Code = x.Code
    };

    private static LocationNodeDto ToLocationDto(Location x) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        QrCode = x.QrCode
    };

    private static SystemSettingDto ToSettingDto(SystemSetting x) => new()
    {
        Id = x.Id,
        Key = x.Key,
        Value = x.Value,
        Description = x.Description
    };
}
