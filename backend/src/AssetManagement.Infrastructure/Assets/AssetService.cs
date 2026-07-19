using AssetManagement.Application.Assets;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;
using System.Security.Claims;

namespace AssetManagement.Infrastructure.Assets;

public class AssetService : IAssetService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;

    // 部门树缓存键
    private const string DepartmentTreeCacheKey = "department_tree";

    public AssetService(AppDbContext db, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

    public async Task<PagedResult<AssetDto>> QueryAsync(AssetQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, AppConstants.MaxPageSize);
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
        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "资产不存在");
        if (asset.IsDeleted)
        {
            throw new BizException(4048, "资产不存在");
        }
        EnsureCanAccess(asset);
        return (await ToDtos(new[] { asset })).Single();
    }

    public async Task<AssetDetailDto> GetDetailAsync(int id)
    {
        // 详情允许查看已删除资产(供主清单中已删除行的"详情"按钮使用),
        // 故不经会拦截已删除的 GetAsync,自行加载实体并保留权限隔离。
        var entity = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "资产不存在");
        EnsureCanAccess(entity);
        var asset = (await ToDtos(new[] { entity })).Single();

        var flows = await _db.ApprovalFlows
            .Where(x => x.AssetId == id)
            .OrderByDescending(x => x.ApplyTime)
            .Select(x => new AssetFlowDto
            {
                Id = x.Id,
                FlowNo = x.FlowNo,
                BizType = x.BizType,
                Status = x.Status,
                Applicant = x.Applicant,
                Transferee = x.Transferee,
                Reason = x.Reason,
                ReturnDate = x.ReturnDate,
                ApplyTime = x.ApplyTime,
                ConfirmedAt = x.ConfirmedAt,
                WithdrawnAt = _db.FlowRecords
                    .Where(record => record.FlowId == x.Id && record.Action == "withdraw")
                    .OrderByDescending(record => record.OperatedAt)
                    .Select(record => (DateTime?)record.OperatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var idText = id.ToString();
        var flowIdTexts = flows.Select(x => x.Id.ToString()).ToArray();
        var logs = await _db.AuditLogs
            .Where(x => (x.TargetType == "Asset" && x.TargetId == idText)
                        || (x.TargetType == "Approval" && x.TargetId != null && flowIdTexts.Contains(x.TargetId)))
            .OrderByDescending(x => x.OccurredAt)
            .Take(100)
            .ToListAsync();
        var userIds = logs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToArray();
        var userNames = await _db.Users
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var recentLogs = logs.Select(x => new AssetAuditLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.UserId.HasValue && userNames.TryGetValue(x.UserId.Value, out var name) ? name : null,
            ActionType = x.ActionType,
            TargetType = x.TargetType,
            TargetId = x.TargetId,
            Summary = x.Summary,
            OccurredAt = x.OccurredAt
        }).ToList();

        return new AssetDetailDto
        {
            Asset = asset,
            Flows = flows,
            RecentLogs = recentLogs
        };
    }

    public async Task<AssetDto> CreateAsync(CreateAssetRequest request)
    {
        EnsureAssetName(request.Name);
        EnsureCanAssignDepartment(request.DepartmentId);
        await EnsureActiveDepartment(request.DepartmentId);
        await EnsureLocationExistsAsync(request.LocationId);
        await EnsureActiveCustodianAsync(request.CustodianId, request.DepartmentId);
        var currentCondition = AssetConditionDictionary.NormalizeSelection(
            request.CurrentCondition,
            await LoadConditionOptionsAsync());
        var category = await _db.AssetCategories.SingleOrDefaultAsync(x => x.Id == request.CategoryId && !x.IsDeleted)
            ?? throw new BizException(4046, "资产分类不存在");

        for (var attempt = 0; ; attempt++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var lockedCategory = await _db.AssetCategories
                .FromSqlInterpolated($"SELECT * FROM asset_categories WHERE Id = {category.Id} FOR UPDATE")
                .SingleOrDefaultAsync()
                ?? throw new BizException(4046, "资产分类不存在");
            var asset = new Asset
            {
                AssetNo = await NextAssetNo(lockedCategory),
                Name = request.Name.Trim(),
                CategoryId = request.CategoryId,
                DepartmentId = request.DepartmentId,
                LocationId = request.LocationId,
                CustodianId = request.CustodianId,
                Quantity = Math.Max(request.Quantity, 1),
                Status = AssetStatus.Available,
                PurchaseDate = request.PurchaseDate,
                RegistrationTime = request.RegistrationTime?.Date ?? BusinessClock.Today,
                CurrentCondition = currentCondition,
                Remark = request.Remark?.Trim(),
                ImageUrls = JoinImages(request.Images),
                CreatedAt = DateTime.UtcNow
            };
            _db.Assets.Add(asset);
            try
            {
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return await GetAsync(asset.Id);
            }
            catch (DbUpdateException ex) when (attempt < 3 && IsDuplicateKey(ex))
            {
                // 资产编号唯一索引冲突（并发取号撞号）：移除失败实体后重新取号重试
                _db.Entry(asset).State = EntityState.Detached;
            }
        }
    }

    public async Task<AssetDto> UpdateAsync(int id, UpdateAssetRequest request)
    {
        EnsureAssetName(request.Name);
        if (!Enum.IsDefined(request.Status))
        {
            throw new BizException(4001, "资产状态无效");
        }
        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "资产不存在");
        if (asset.IsDeleted) throw new BizException(4048, "资产不存在");
        EnsureCanAccess(asset);
        if (request.Status != asset.Status || request.CustodianId != asset.CustodianId || request.DepartmentId != asset.DepartmentId)
            throw new BizException(4095, "资产状态、保管人和归属部门只能通过审批流转变更");
        if (!await _db.AssetCategories.AnyAsync(x => x.Id == request.CategoryId && !x.IsDeleted))
        {
            throw new BizException(4046, "资产分类不存在");
        }
        await EnsureLocationExistsAsync(request.LocationId);

        asset.Name = request.Name.Trim();
        asset.CategoryId = request.CategoryId;
        asset.LocationId = request.LocationId;
        asset.Quantity = Math.Max(request.Quantity, 1);
        asset.PurchaseDate = request.PurchaseDate;
        asset.RegistrationTime = request.RegistrationTime?.Date;
        asset.CurrentCondition = AssetConditionDictionary.NormalizeSelection(
            request.CurrentCondition,
            await LoadConditionOptionsAsync(),
            asset.CurrentCondition);
        asset.Remark = request.Remark?.Trim();
        if (request.Images is not null)
        {
            asset.ImageUrls = JoinImages(request.Images);
        }
        asset.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "资产已被其他操作更新，请刷新后重试");
        }
        return await GetAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var asset = await _db.Assets
            .FromSqlInterpolated($"SELECT * FROM assets WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4048, "资产不存在");
        if (asset.IsDeleted)
        {
            throw new BizException(4048, "资产不存在");
        }
        EnsureCanAccess(asset);
        if (asset.Status == AssetStatus.Borrowed)
        {
            throw new BizException(4092, "借出中资产不能删除");
        }
        if (await _db.ApprovalFlows.AnyAsync(x => x.AssetId == id && x.Status == "pending"))
        {
            throw new BizException(4094, "资产存在待审批流转，不能删除");
        }

        asset.IsDeleted = true;
        asset.DeletedAt = DateTime.UtcNow;
        asset.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "资产已被其他操作更新，请刷新后重试");
        }
        await transaction.CommitAsync();
    }

    public async Task PurgeAsync(int id)
    {
        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "资产不存在");
        EnsureCanAccess(asset);
        if (!asset.IsDeleted)
        {
            throw new BizException(4097, "请先删除资产后再彻底删除");
        }
        if (await _db.ApprovalFlows.AnyAsync(x => x.AssetId == id))
        {
            throw new BizException(4094, "资产存在流转历史，不能彻底删除");
        }

        _db.Assets.Remove(asset);
        asset.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "资产已被其他操作更新，请刷新后重试");
        }
    }

    public async Task RestoreAsync(int id)
    {
        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "资产不存在");
        EnsureCanAccess(asset);
        if (!asset.IsDeleted)
        {
            throw new BizException(4099, "资产未删除，无需恢复");
        }
        if (!await _db.AssetCategories.AnyAsync(x => x.Id == asset.CategoryId && !x.IsDeleted))
        {
            throw new BizException(4094, "资产所属分类已删除，请先恢复分类");
        }

        asset.IsDeleted = false;
        asset.DeletedAt = null;
        asset.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "资产已被其他操作更新，请刷新后重试");
        }
    }

    public async Task<byte[]> ExportAsync(AssetQuery query)
    {
        var rows = new List<string[]>
        {
            new[] { "资产编号", "名称", "分类编码", "部门", "位置", "数量", "状态", "购入日期", "资产登记日期", "目前状况", "备注" }
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
            x.Quantity.ToString(),
            x.Status.ToString(),
            x.PurchaseDate?.ToString("yyyy-MM-dd") ?? "",
            x.RegistrationTime?.ToString("yyyy-MM-dd") ?? "",
            x.CurrentCondition ?? "",
            x.Remark ?? ""
        }));
        return XlsxTable.Write(rows);
    }

    public byte[] BuildImportTemplate()
        => XlsxTable.Write(new[]
        {
            new[] { "名称", "分类编码", "购入日期", "资产登记日期", "目前状况", "备注" }
        });

    public async Task<List<ImportPreviewRow>> ValidateImportAsync(Stream file)
    {
        var rows = XlsxTable.Read(file).Skip(1).ToList();
        if (rows.Count > AppConstants.MaxImportRows)
        {
            throw new BizException(4153, $"单次导入不能超过 {AppConstants.MaxImportRows} 行");
        }
        var categories = await _db.AssetCategories.Where(x => !x.IsDeleted).ToDictionaryAsync(x => x.Code, x => x);
        var conditionOptions = await LoadConditionOptionsAsync();
        return rows.Select((cells, index) => ValidateRow(index + 2, cells, categories, conditionOptions)).ToList();
    }

    public async Task<ImportConfirmResult> ConfirmImportAsync(Stream file)
    {
        var rows = await ValidateImportAsync(file);
        var validRows = rows.Where(x => x.IsValid).ToList();
        var departmentId = CurrentUserDepartmentId();
        var categoryCache = new Dictionary<string, AssetCategory>();
        var seq = new Dictionary<int, int>();

        // 整批一个事务,任一失败整体回滚,避免逐条提交产生半残数据
        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var categoryCode in validRows.Select(x => x.CategoryCode).Distinct().OrderBy(x => x))
        {
            var lockedCategory = await _db.AssetCategories
                .FromSqlInterpolated($"SELECT * FROM asset_categories WHERE Code = {categoryCode} AND IsDeleted = 0 FOR UPDATE")
                .SingleAsync();
            categoryCache[categoryCode] = lockedCategory;
        }
        foreach (var row in validRows)
        {
            var category = categoryCache[row.CategoryCode];
            // 同分类多行在内存中递增取号:批量提交前 Count 不变,直接用会撞唯一索引
            if (!seq.TryGetValue(category.Id, out var used))
            {
                used = await CurrentMaxSequence(category);
            }
            seq[category.Id] = used + 1;

            _db.Assets.Add(new Asset
            {
                AssetNo = AssetNoGenerator.Next(category.Code, used),
                Name = row.Name,
                CategoryId = category.Id,
                DepartmentId = departmentId,
                PurchaseDate = row.PurchaseDate,
                RegistrationTime = row.RegistrationTime?.Date ?? BusinessClock.Today,
                CurrentCondition = row.CurrentCondition,
                Remark = row.Remark,
                Quantity = 1,
                Status = AssetStatus.Available,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new ImportConfirmResult
        {
            SuccessCount = validRows.Count,
            FailedCount = rows.Count - validRows.Count,
            Rows = rows
        };
    }

    private IQueryable<Asset> ApplyQuery(IQueryable<Asset> queryable, AssetQuery query)
    {
        var deleteStatus = query.DeletedOnly
            ? "deleted"
            : query.DeleteStatus?.Trim().ToLowerInvariant();
        queryable = deleteStatus switch
        {
            "all" => queryable,
            "deleted" => queryable.Where(x => x.IsDeleted),
            _ => queryable.Where(x => !x.IsDeleted)
        };

        // 部门数据权限隔离:部门主管只能查看本部门及子部门资产(系统管理员/普通员工不受限)
        var allowedDepartments = AllowedDepartmentIds();
        if (allowedDepartments != null)
        {
            queryable = queryable.Where(x => x.DepartmentId.HasValue && allowedDepartments.Contains(x.DepartmentId.Value));
        }

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

        if (query.CustodianId.HasValue)
        {
            queryable = queryable.Where(x => x.CustodianId == query.CustodianId.Value);
        }

        return queryable;
    }

    private int[] DescendantDepartmentIds(int rootId)
    {
        // 从缓存获取部门树
        var departments = _cache.GetOrCreate(DepartmentTreeCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AppConstants.DepartmentTreeCacheMinutes);
            return _db.Departments.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToList();
        })!;

        var ids = new HashSet<int> { rootId };
        void Walk(int parentId)
        {
            foreach (var child in departments.Where(x => x.ParentId == parentId))
            {
                if (ids.Add(child.Id))
                {
                    Walk(child.Id);
                }
            }
        }

        Walk(rootId);
        return ids.ToArray();
    }

    // 返回当前用户允许访问的部门 ID 集合;null 表示不受限(超级管理员或普通员工共享池)
    private int[]? AllowedDepartmentIds()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Contains("admin"))
        {
            return null;
        }
        if (roles.Contains("supervisor"))
        {
            var deptIdClaim = user.FindFirst("departmentId")?.Value;
            if (int.TryParse(deptIdClaim, out var userDeptId))
            {
                return DescendantDepartmentIds(userDeptId);
            }
            return Array.Empty<int>();
        }
        return null;
    }

    // 校验当前用户是否有权访问指定资产(部门主管越权访问其他部门资产时抛出)
    private void EnsureCanAccess(Asset asset)
    {
        var allowed = AllowedDepartmentIds();
        if (allowed != null && (!asset.DepartmentId.HasValue || !allowed.Contains(asset.DepartmentId.Value)))
        {
            throw new BizException(4047, "无权访问该资产");
        }
    }

    // 校验当前用户是否有权将资产归属到目标部门(防止部门主管把资产划入/划出无权部门)
    private void EnsureCanAssignDepartment(int? departmentId)
    {
        var allowed = AllowedDepartmentIds();
        if (allowed != null && (!departmentId.HasValue || !allowed.Contains(departmentId.Value)))
        {
            throw new BizException(4047, "无权将资产归属到该部门");
        }
    }

    private async Task EnsureActiveDepartment(int? departmentId)
    {
        if (!departmentId.HasValue)
        {
            return;
        }

        if (!await _db.Departments.AnyAsync(x => x.Id == departmentId.Value && x.IsActive))
        {
            throw new BizException(4045, "部门不存在或已停用");
        }
    }

    private async Task EnsureLocationExistsAsync(int? locationId)
    {
        if (locationId.HasValue && !await _db.Locations.AnyAsync(x => x.Id == locationId.Value))
        {
            throw new BizException(4045, "存放位置不存在");
        }
    }

    private async Task EnsureActiveCustodianAsync(int? custodianId, int? departmentId)
    {
        if (!custodianId.HasValue)
        {
            return;
        }
        var custodian = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == custodianId.Value && x.IsActive)
            ?? throw new BizException(4041, "保管人不存在或已停用");
        if (custodian.DepartmentId != departmentId)
        {
            throw new BizException(4002, "保管人与归属部门不一致");
        }
    }

    // 当前用户所属部门(用于导入资产的部门归属)
    private int? CurrentUserDepartmentId()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst("departmentId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private async Task<string> NextAssetNo(AssetCategory category)
        => AssetNoGenerator.Next(category.Code, await CurrentMaxSequence(category));

    private async Task<int> CurrentMaxSequence(AssetCategory category)
    {
        var prefix = $"{category.Code}-";
        var existing = await _db.Assets
            .Where(x => x.CategoryId == category.Id && x.AssetNo.StartsWith(prefix))
            .Select(x => x.AssetNo)
            .ToListAsync();
        var maxSequence = existing
            .Select(x => int.TryParse(x[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();
        return maxSequence;
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is MySqlException { Number: 1062 };

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
                DepartmentId = x.DepartmentId,
                DepartmentName = x.DepartmentId.HasValue && departments.TryGetValue(x.DepartmentId.Value, out var dept) ? dept : null,
                LocationId = x.LocationId,
                LocationName = x.LocationId.HasValue && locations.TryGetValue(x.LocationId.Value, out var loc) ? loc : null,
                CustodianId = x.CustodianId,
                CustodianName = x.CustodianId.HasValue && custodians.TryGetValue(x.CustodianId.Value, out var custodian) ? custodian : null,
                Quantity = x.Quantity,
                Status = x.Status,
                PurchaseDate = x.PurchaseDate,
                RegistrationTime = x.RegistrationTime?.Date,
                CurrentCondition = x.CurrentCondition,
                Remark = x.Remark,
                CreatedAt = x.CreatedAt,
                IsDeleted = x.IsDeleted,
                DeletedAt = x.DeletedAt,
                Images = SplitImages(x.ImageUrls)
            };
        }).ToList();
    }

    private static string? JoinImages(IEnumerable<string>? images) => ImageHelpers.Join(images);

    private static List<string> SplitImages(string? imageUrls) => ImageHelpers.Split(imageUrls);

    private static void EnsureAssetName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BizException(4001, "资产名称必填");
        }
        if (name.Trim().Length > 100)
        {
            throw new BizException(4001, "资产名称不能超过 100 个字符");
        }
    }

    private static ImportPreviewRow ValidateRow(
        int rowNumber,
        IReadOnlyList<string> cells,
        Dictionary<string, AssetCategory> categories,
        IReadOnlyList<string> conditionOptions)
    {
        var name = Cell(cells, 0);
        var categoryCode = Cell(cells, 1);
        var purchaseDateText = Cell(cells, 2);
        var registrationTimeText = Cell(cells, 3);
        var currentConditionText = Cell(cells, 4);
        var remark = Cell(cells, 5);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("名称必填");
        if (string.IsNullOrWhiteSpace(categoryCode) || !categories.ContainsKey(categoryCode)) errors.Add("分类编码不存在");
        var purchaseDate = ParseOptionalDate(purchaseDateText, "购入日期", errors);
        var registrationTime = ParseOptionalDate(registrationTimeText, "资产登记日期", errors);
        var currentCondition = string.IsNullOrWhiteSpace(currentConditionText)
            ? null
            : conditionOptions.FirstOrDefault(x => x.Equals(currentConditionText.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(currentConditionText) && currentCondition is null)
        {
            errors.Add($"目前状况「{currentConditionText.Trim()}」不在数据字典中");
        }

        return new ImportPreviewRow
        {
            Row = rowNumber,
            Name = name,
            CategoryCode = categoryCode,
            PurchaseDate = purchaseDate,
            RegistrationTime = registrationTime,
            CurrentCondition = currentCondition,
            Remark = remark,
            IsValid = errors.Count == 0,
            Error = string.Join("；", errors)
        };
    }

    private static DateTime? ParseOptionalDate(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, out var parsed)) return parsed.Date;
        errors.Add($"{field}格式不正确");
        return null;
    }

    private async Task<IReadOnlyList<string>> LoadConditionOptionsAsync()
    {
        var raw = await _db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == AssetConditionDictionary.SettingKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync();
        return AssetConditionDictionary.ParseOrDefault(raw);
    }

    private static string Cell(IReadOnlyList<string> cells, int index)
        => index < cells.Count ? cells[index].Trim() : "";
}
