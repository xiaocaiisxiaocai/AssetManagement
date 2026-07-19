using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace AssetManagement.Infrastructure.TestMaterials;

public class TestMaterialService : ITestMaterialService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IMemoryCache _cache;
    private const string DepartmentTreeCacheKey = "department_tree";

    public TestMaterialService(AppDbContext db, IHttpContextAccessor http, IMemoryCache cache)
    {
        _db = db;
        _http = http;
        _cache = cache;
    }

    public async Task<PagedResult<TestMaterialDto>> QueryAsync(TestMaterialQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var q = await ApplyQueryAsync(_db.TestMaterials.AsQueryable(), query);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<TestMaterialDto>
        {
            Items = await ToDtos(items),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TestMaterialDto> GetAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        if (m.IsDeleted) throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(m);
        return (await ToDtos(new[] { m })).Single();
    }

    public async Task<TestMaterialDetailDto> GetDetailAsync(int id)
    {
        // 详情允许查看已删除料件(供主清单已删除行的"详情"按钮)
        var entity = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(entity);
        var material = (await ToDtos(new[] { entity })).Single();

        var flows = await _db.MaterialFlows
            .Where(x => x.MaterialId == id)
            .OrderByDescending(x => x.ApplyTime)
            .Select(x => new MaterialFlowDto
            {
                Id = x.Id,
                FlowNo = x.FlowNo,
                BizType = x.BizType,
                MaterialId = x.MaterialId,
                MaterialNo = x.MaterialNo,
                MaterialName = x.MaterialName,
                Applicant = x.Applicant,
                ApplicantDept = x.ApplicantDept,
                Transferee = x.Transferee,
                TransfereeDept = x.TransfereeDept,
                Reason = x.Reason,
                Status = x.Status,
                ApplyTime = x.ApplyTime,
                Deadline = x.Deadline
            })
            .ToListAsync();

        var flowIds = flows.Select(f => f.Id).ToArray();
        var records = await _db.MaterialFlowRecords
            .Where(x => flowIds.Contains(x.FlowId))
            .OrderByDescending(x => x.OperatedAt)
            .Select(x => new MaterialFlowRecordDto
            {
                Id = x.Id,
                Action = x.Action,
                Operator = x.Operator,
                Comment = x.Comment,
                OperatedAt = x.OperatedAt
            })
            .ToListAsync();

        return new TestMaterialDetailDto { Material = material, Flows = flows, Records = records };
    }

    public async Task<TestMaterialDto> CreateAsync(SaveTestMaterialRequest request)
    {
        await EnsureCanAssignDepartmentAsync(request.DepartmentId);
        await EnsureActiveDepartmentAsync(request.DepartmentId);
        await EnsureLocationExistsAsync(request.LocationId);
        await EnsureActiveCustodianAsync(request.CustodianId, request.DepartmentId);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new BizException(4001, "请填写料件名称");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {request.ProjectId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync();
        if (project is null || project.IsDeleted)
            throw new BizException(4046, "测试项目不存在");
        await EnsureCanWriteMaterialAsync(project, "material:create");
        await EnsureMaterialNameAvailableAsync(request.ProjectId, name);
        var m = new TestMaterial
        {
                MaterialNo = await NextMaterialNo(),
                Name = name,
                ProjectId = request.ProjectId,
                VendorName = request.VendorName?.Trim(),
                Model = request.Model?.Trim(),
                Brand = request.Brand?.Trim(),
                Quantity = Math.Max(request.Quantity, 1),
                DepartmentId = request.DepartmentId,
                LocationId = request.LocationId,
                CustodianId = request.CustodianId,
                ReceivedDate = request.ReceivedDate,
                Status = MaterialStatus.InUse,
                ImageUrls = JoinImages(request.Images),
                Remark = request.Remark?.Trim(),
                CreatedAt = DateTime.UtcNow
        };
        _db.TestMaterials.Add(m);
        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return await GetAsync(m.Id);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "同一项目下的料件名称已存在");
        }
    }

    public async Task<TestMaterialDto> UpdateAsync(int id, SaveTestMaterialRequest request)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        if (m.IsDeleted) throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(m);
        EnsureInUse(m, "已退回厂商的料件不能编辑");
        if (request.DepartmentId != m.DepartmentId || request.CustodianId != m.CustodianId)
            throw new BizException(4095, "料件保管人和归属部门只能通过流转变更");
        if (request.ProjectId != m.ProjectId)
            throw new BizException(4095, "料件所属项目不能修改");
        await EnsureLocationExistsAsync(request.LocationId);
        var originalProject = await _db.TestProjects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == m.ProjectId && !x.IsDeleted)
            ?? throw new BizException(4046, "测试项目不存在");
        await EnsureCanWriteMaterialAsync(originalProject, "material:edit");
        if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == id && x.Status == "pending"))
            throw new BizException(4092, "该料件有进行中的流转,不能编辑");
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new BizException(4001, "请填写料件名称");
        await EnsureMaterialNameAvailableAsync(request.ProjectId, name, id);

        m.Name = name;
        m.ProjectId = request.ProjectId;
        m.VendorName = request.VendorName?.Trim();
        m.Model = request.Model?.Trim();
        m.Brand = request.Brand?.Trim();
        m.Quantity = Math.Max(request.Quantity, 1);
        m.LocationId = request.LocationId;
        m.ReceivedDate = request.ReceivedDate;
        m.Remark = request.Remark?.Trim();
        if (request.Images is not null) m.ImageUrls = JoinImages(request.Images);
        m.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "料件已被其他操作更新，请刷新后重试");
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "同一项目下的料件名称已存在");
        }
        return await GetAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        if (m.IsDeleted) throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(m);
        EnsureInUse(m, "已退回厂商的料件不能删除");
        if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == id && x.Status == "pending"))
            throw new BizException(4092, "该料件有进行中的流转,不能删除");
        m.IsDeleted = true;
        m.DeletedAt = DateTime.UtcNow;
        m.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "料件已被其他操作更新，请刷新后重试");
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "同一项目下的料件名称已存在");
        }
    }

    public async Task RestoreAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(m);
        if (!m.IsDeleted) throw new BizException(4099, "料件未删除,无需恢复");
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {m.ProjectId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync();
        if (project is null || project.IsDeleted)
            throw new BizException(4094, "料件所属项目已删除，请先恢复项目");
        m.IsDeleted = false;
        m.DeletedAt = null;
        m.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "料件已被其他操作更新，请刷新后重试");
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "同一项目下已有同名活动料件，不能恢复");
        }
        await transaction.CommitAsync();
    }

    public async Task PurgeAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(m);
        if (!m.IsDeleted) throw new BizException(4097, "请先删除料件后再彻底删除");
        if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == id))
            throw new BizException(4094, "料件存在流转历史，不能彻底删除");
        _db.TestMaterials.Remove(m);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "料件已被其他操作更新，请刷新后重试");
        }
    }

    public async Task<TestMaterialDto> ReturnToVendorAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        if (m.IsDeleted) throw new BizException(4048, "测试料件不存在");
        await EnsureCanAccessAsync(m);
        if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == id && x.Status == "pending"))
            throw new BizException(4092, "该料件有进行中的流转,不能退回厂商");
        if (m.Status == MaterialStatus.ReturnedToVendor)
            throw new BizException(4099, "料件已退回厂商,无需重复操作");
        m.Status = MaterialStatus.ReturnedToVendor;
        m.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "料件已被其他操作更新，请刷新后重试");
        }
        return await GetAsync(id);
    }

    // ===== 部门隔离(逻辑比照 AssetService.ApplyQuery) =====
    private async Task<IQueryable<TestMaterial>> ApplyQueryAsync(IQueryable<TestMaterial> q, TestMaterialQuery query)
    {
        var deleteStatus = query.DeleteStatus?.Trim().ToLowerInvariant();
        q = deleteStatus switch
        {
            "all" => q,
            "deleted" => q.Where(x => x.IsDeleted),
            _ => q.Where(x => !x.IsDeleted)
        };

        var allowed = await AllowedDepartmentIdsAsync();
        if (allowed != null)
            q = q.Where(x => x.DepartmentId.HasValue && allowed.Contains(x.DepartmentId.Value));

        if (!string.IsNullOrWhiteSpace(query.MaterialNo))
        {
            var no = query.MaterialNo.Trim();
            q = q.Where(x => x.MaterialNo.Contains(no));
        }
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim();
            q = q.Where(x => x.Name.Contains(name));
        }
        if (query.ProjectId.HasValue) q = q.Where(x => x.ProjectId == query.ProjectId.Value);
        if (query.Status.HasValue) q = q.Where(x => x.Status == query.Status.Value);
        if (query.DepartmentId.HasValue)
        {
            var ids = await DescendantDepartmentIdsAsync(query.DepartmentId.Value);
            q = q.Where(x => x.DepartmentId.HasValue && ids.Contains(x.DepartmentId.Value));
        }
        return q;
    }

    // 与 AssetService.DescendantDepartmentIds 共享部门树缓存键；两处隔离口径必须保持一致。
    private async Task<int[]> DescendantDepartmentIdsAsync(int rootId)
    {
        var departments = await _cache.GetOrCreateAsync(DepartmentTreeCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AppConstants.DepartmentTreeCacheMinutes);
            return await _db.Departments.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToListAsync();
        });
        var ids = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        while (queue.TryDequeue(out var parentId))
        {
            foreach (var child in departments!.Where(x => x.ParentId == parentId))
            {
                if (ids.Add(child.Id)) queue.Enqueue(child.Id);
            }
        }
        return ids.ToArray();
    }

    private async Task<int[]?> AllowedDepartmentIdsAsync()
    {
        var user = _http.HttpContext?.User;
        if (user is null) return null;
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Contains("admin")) return null;
        if (roles.Contains("supervisor"))
        {
            var deptIdClaim = user.FindFirst("departmentId")?.Value;
            if (int.TryParse(deptIdClaim, out var deptId))
                return await DescendantDepartmentIdsAsync(deptId);
            return Array.Empty<int>();
        }
        return null;
    }

    private async Task EnsureCanAccessAsync(TestMaterial m)
    {
        var allowed = await AllowedDepartmentIdsAsync();
        if (allowed != null && (!m.DepartmentId.HasValue || !allowed.Contains(m.DepartmentId.Value)))
            throw new BizException(4047, "无权访问该测试料件");
    }

    private async Task EnsureCanAssignDepartmentAsync(int? departmentId)
    {
        var allowed = await AllowedDepartmentIdsAsync();
        if (allowed != null && (!departmentId.HasValue || !allowed.Contains(departmentId.Value)))
            throw new BizException(4047, "无权将料件归属到该部门");
    }

    private async Task EnsureActiveDepartmentAsync(int? departmentId)
    {
        if (!departmentId.HasValue) return;
        if (!await _db.Departments.AnyAsync(x => x.Id == departmentId.Value && x.IsActive))
            throw new BizException(4045, "部门不存在或已停用");
    }

    private async Task EnsureLocationExistsAsync(int? locationId)
    {
        if (!locationId.HasValue) return;
        if (!await _db.Locations.AnyAsync(x => x.Id == locationId.Value))
            throw new BizException(4045, "存放位置不存在");
    }

    private async Task EnsureActiveCustodianAsync(int? custodianId, int? departmentId)
    {
        if (!custodianId.HasValue) return;
        var custodian = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == custodianId.Value && x.IsActive)
            ?? throw new BizException(4041, "保管人不存在或已停用");
        if (custodian.DepartmentId != departmentId)
            throw new BizException(4002, "保管人与归属部门不一致");
    }

    private async Task EnsureMaterialNameAvailableAsync(int projectId, string name, int? selfId = null)
    {
        if (await _db.TestMaterials.AnyAsync(x =>
                x.ProjectId == projectId &&
                x.Name == name &&
                !x.IsDeleted &&
                x.Id != selfId))
        {
            throw new BizException(4094, "料件名称已存在");
        }
    }

    private async Task EnsureCanWriteMaterialAsync(TestProject project, string permission)
    {
        // 料件维护既允许拥有菜单权限的角色操作，也允许项目负责人维护自己负责项目下的料件。
        var user = _http.HttpContext?.User;
        var currentUserId = CurrentUserId();
        var permissions = user?.FindAll("perm").Select(x => x.Value).ToArray() ?? Array.Empty<string>();
        if (user?.IsInRole("admin") == true || permissions.Contains(permission) || project.OwnerId == currentUserId)
            return;
        throw new BizException(4047, "无权维护该项目料件");
    }

    private int? CurrentUserId()
    {
        var value = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static void EnsureInUse(TestMaterial material, string message)
    {
        if (material.Status != MaterialStatus.InUse)
            throw new BizException(4098, message);
    }

    private async Task<string> NextMaterialNo()
    {
        var today = BusinessClock.Today;
        var prefix = $"TM-{today:yyyyMMdd}-";
        var existing = await _db.TestMaterials
            .Where(x => x.MaterialNo.StartsWith(prefix))
            .Select(x => x.MaterialNo)
            .ToListAsync();
        var maxSequence = existing
            .Select(x => int.TryParse(x[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();
        var sequence = await BusinessSequenceGenerator.NextAsync(
            _db, $"test-material:{today:yyyyMMdd}", maxSequence);
        return $"{prefix}{sequence:D3}";
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is MySqlException { Number: 1062 };

    private async Task<List<TestMaterialDto>> ToDtos(IEnumerable<TestMaterial> materials)
    {
        var list = materials.ToList();
        var projectIds = list.Select(x => x.ProjectId).Distinct().ToArray();
        var deptIds = list.Where(x => x.DepartmentId.HasValue).Select(x => x.DepartmentId!.Value).Distinct().ToArray();
        var locIds = list.Where(x => x.LocationId.HasValue).Select(x => x.LocationId!.Value).Distinct().ToArray();
        var custodianIds = list.Where(x => x.CustodianId.HasValue).Select(x => x.CustodianId!.Value).Distinct().ToArray();
        var ids = list.Select(x => x.Id).ToArray();

        var projects = await _db.TestProjects.Where(x => projectIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var depts = await _db.Departments.Where(x => deptIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var locs = await _db.Locations.Where(x => locIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var custodians = await _db.Users.Where(x => custodianIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var pendingMaterialIds = await _db.MaterialFlows
            .Where(x => x.Status == "pending" && ids.Contains(x.MaterialId))
            .Select(x => x.MaterialId).Distinct().ToListAsync();
        var pendingSet = pendingMaterialIds.ToHashSet();

        return list.Select(x => new TestMaterialDto
        {
            Id = x.Id,
            MaterialNo = x.MaterialNo,
            Name = x.Name,
            ProjectId = x.ProjectId,
            ProjectName = projects.GetValueOrDefault(x.ProjectId),
            VendorName = x.VendorName,
            Model = x.Model,
            Brand = x.Brand,
            Quantity = x.Quantity,
            DepartmentId = x.DepartmentId,
            DepartmentName = x.DepartmentId.HasValue ? depts.GetValueOrDefault(x.DepartmentId.Value) : null,
            LocationId = x.LocationId,
            LocationName = x.LocationId.HasValue ? locs.GetValueOrDefault(x.LocationId.Value) : null,
            CustodianId = x.CustodianId,
            CustodianName = x.CustodianId.HasValue ? custodians.GetValueOrDefault(x.CustodianId.Value) : null,
            ReceivedDate = x.ReceivedDate,
            Status = x.Status,
            Images = SplitImages(x.ImageUrls),
            Remark = x.Remark,
            CreatedAt = x.CreatedAt,
            IsDeleted = x.IsDeleted,
            DeletedAt = x.DeletedAt,
            HasPendingFlow = pendingSet.Contains(x.Id)
        }).ToList();
    }

    private static string? JoinImages(IEnumerable<string>? images) => ImageHelpers.Join(images);

    private static List<string> SplitImages(string? imageUrls) => ImageHelpers.Split(imageUrls);
}
