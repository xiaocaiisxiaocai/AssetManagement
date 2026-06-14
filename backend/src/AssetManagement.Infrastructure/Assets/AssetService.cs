using AssetManagement.Application.Assets;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Assets;

public class AssetService : IAssetService
{
    private readonly AppDbContext _db;

    public AssetService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AssetDto>> QueryAsync(AssetQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var assets = ApplyQuery(_db.Assets.AsQueryable(), query);
        var total = await assets.CountAsync();
        var pageItems = await assets
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AssetDto>
        {
            Items = await ToDtos(pageItems),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AssetDto> GetAsync(int id)
    {
        var asset = await _db.Assets.FindAsync(id)
            ?? throw new BizException(4048, "资产不存在");
        return (await ToDtos(new[] { asset })).Single();
    }

    public async Task<AssetDto> CreateAsync(CreateAssetRequest request)
    {
        var category = await _db.AssetCategories.FindAsync(request.CategoryId)
            ?? throw new BizException(4046, "资产分类不存在");
        var asset = new Asset
        {
            AssetNo = await NextAssetNo(category),
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            DepartmentId = request.DepartmentId,
            LocationId = request.LocationId,
            CustodianId = request.CustodianId,
            Model = request.Model,
            Brand = request.Brand,
            Price = request.Price,
            Quantity = Math.Max(request.Quantity, 1),
            Status = AssetStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        return await GetAsync(asset.Id);
    }

    public async Task<AssetDto> UpdateAsync(int id, UpdateAssetRequest request)
    {
        var asset = await _db.Assets.FindAsync(id)
            ?? throw new BizException(4048, "资产不存在");
        if (!await _db.AssetCategories.AnyAsync(x => x.Id == request.CategoryId))
        {
            throw new BizException(4046, "资产分类不存在");
        }

        asset.Name = request.Name.Trim();
        asset.CategoryId = request.CategoryId;
        asset.DepartmentId = request.DepartmentId;
        asset.LocationId = request.LocationId;
        asset.CustodianId = request.CustodianId;
        asset.Model = request.Model;
        asset.Brand = request.Brand;
        asset.Price = request.Price;
        asset.Quantity = Math.Max(request.Quantity, 1);
        asset.Status = request.Status;
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var asset = await _db.Assets.FindAsync(id)
            ?? throw new BizException(4048, "资产不存在");
        if (asset.Status == AssetStatus.Borrowed)
        {
            throw new BizException(4092, "借出中资产不能删除");
        }

        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync();
    }

    public async Task<byte[]> ExportAsync(AssetQuery query)
    {
        var rows = new List<string[]>
        {
            new[] { "资产编号", "名称", "分类编码", "部门", "位置", "型号", "品牌", "单价", "数量", "状态" }
        };
        var assets = await ApplyQuery(_db.Assets.AsQueryable(), query)
            .OrderBy(x => x.AssetNo)
            .ToListAsync();
        var dtos = await ToDtos(assets);
        rows.AddRange(dtos.Select(x => new[]
        {
            x.AssetNo,
            x.Name,
            x.CategoryCode,
            x.DepartmentName ?? "",
            x.LocationName ?? "",
            x.Model ?? "",
            x.Brand ?? "",
            x.Price.ToString("0.##"),
            x.Quantity.ToString(),
            x.Status.ToString()
        }));
        return XlsxTable.Write(rows);
    }

    public byte[] BuildImportTemplate()
        => XlsxTable.Write(new[]
        {
            new[] { "名称", "分类编码", "型号", "品牌", "单价" }
        });

    public async Task<List<ImportPreviewRow>> ValidateImportAsync(Stream file)
    {
        var rows = XlsxTable.Read(file).Skip(1).ToList();
        var categories = await _db.AssetCategories.ToDictionaryAsync(x => x.Code, x => x);
        return rows.Select((cells, index) => ValidateRow(index + 2, cells, categories)).ToList();
    }

    public async Task<ImportConfirmResult> ConfirmImportAsync(Stream file)
    {
        var rows = await ValidateImportAsync(file);
        var validRows = rows.Where(x => x.IsValid).ToList();
        foreach (var row in validRows)
        {
            var category = await _db.AssetCategories.SingleAsync(x => x.Code == row.CategoryCode);
            _db.Assets.Add(new Asset
            {
                AssetNo = await NextAssetNo(category),
                Name = row.Name,
                CategoryId = category.Id,
                Model = row.Model,
                Brand = row.Brand,
                Price = row.Price,
                Quantity = 1,
                Status = AssetStatus.Available,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return new ImportConfirmResult
        {
            SuccessCount = validRows.Count,
            FailedCount = rows.Count - validRows.Count,
            Rows = rows
        };
    }

    private IQueryable<Asset> ApplyQuery(IQueryable<Asset> queryable, AssetQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.AssetNo))
        {
            var assetNo = query.AssetNo.Trim();
            queryable = queryable.Where(x => x.AssetNo.Contains(assetNo));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim();
            queryable = queryable.Where(x => x.Name.Contains(name));
        }

        if (query.CategoryId.HasValue)
        {
            queryable = queryable.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(x => x.Status == query.Status.Value);
        }

        if (query.DepartmentId.HasValue)
        {
            var departmentIds = DescendantDepartmentIds(query.DepartmentId.Value);
            queryable = queryable.Where(x => x.DepartmentId.HasValue && departmentIds.Contains(x.DepartmentId.Value));
        }

        return queryable;
    }

    private int[] DescendantDepartmentIds(int rootId)
    {
        var departments = _db.Departments.ToList();
        var ids = new List<int> { rootId };
        void Walk(int parentId)
        {
            foreach (var child in departments.Where(x => x.ParentId == parentId))
            {
                ids.Add(child.Id);
                Walk(child.Id);
            }
        }

        Walk(rootId);
        return ids.ToArray();
    }

    private async Task<string> NextAssetNo(AssetCategory category)
    {
        var count = await _db.Assets.CountAsync(x => x.CategoryId == category.Id);
        return AssetNoGenerator.Next(category.Code, count);
    }

    private async Task<List<AssetDto>> ToDtos(IEnumerable<Asset> assets)
    {
        var list = assets.ToList();
        var categoryIds = list.Select(x => x.CategoryId).Distinct().ToArray();
        var departmentIds = list.Where(x => x.DepartmentId.HasValue).Select(x => x.DepartmentId!.Value).Distinct().ToArray();
        var locationIds = list.Where(x => x.LocationId.HasValue).Select(x => x.LocationId!.Value).Distinct().ToArray();
        var custodianIds = list.Where(x => x.CustodianId.HasValue).Select(x => x.CustodianId!.Value).Distinct().ToArray();
        var categories = await _db.AssetCategories.Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var departments = await _db.Departments.Where(x => departmentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var locations = await _db.Locations.Where(x => locationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var custodians = await _db.Users.Where(x => custodianIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);

        return list.Select(x =>
        {
            categories.TryGetValue(x.CategoryId, out var category);
            return new AssetDto
            {
                Id = x.Id,
                AssetNo = x.AssetNo,
                Name = x.Name,
                CategoryId = x.CategoryId,
                CategoryCode = category?.Code ?? "",
                CategoryName = category?.Name ?? "",
                DepartmentId = x.DepartmentId,
                DepartmentName = x.DepartmentId.HasValue && departments.TryGetValue(x.DepartmentId.Value, out var dept) ? dept : null,
                LocationId = x.LocationId,
                LocationName = x.LocationId.HasValue && locations.TryGetValue(x.LocationId.Value, out var loc) ? loc : null,
                CustodianId = x.CustodianId,
                CustodianName = x.CustodianId.HasValue && custodians.TryGetValue(x.CustodianId.Value, out var custodian) ? custodian : null,
                Model = x.Model,
                Brand = x.Brand,
                Price = x.Price,
                Quantity = x.Quantity,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            };
        }).ToList();
    }

    private static ImportPreviewRow ValidateRow(int rowNumber, IReadOnlyList<string> cells, Dictionary<string, AssetCategory> categories)
    {
        var name = Cell(cells, 0);
        var categoryCode = Cell(cells, 1);
        var model = Cell(cells, 2);
        var brand = Cell(cells, 3);
        var priceText = Cell(cells, 4);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("名称必填");
        if (string.IsNullOrWhiteSpace(categoryCode) || !categories.ContainsKey(categoryCode)) errors.Add("分类编码不存在");
        if (!decimal.TryParse(priceText, out var price)) errors.Add("单价必须为数字");

        return new ImportPreviewRow
        {
            Row = rowNumber,
            Name = name,
            CategoryCode = categoryCode,
            Model = model,
            Brand = brand,
            Price = price,
            IsValid = errors.Count == 0,
            Error = string.Join("；", errors)
        };
    }

    private static string Cell(IReadOnlyList<string> cells, int index)
        => index < cells.Count ? cells[index].Trim() : "";
}
