using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

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
        var q = ApplyQuery(_db.TestMaterials.AsQueryable(), query);
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
        EnsureCanAccess(m);
        return (await ToDtos(new[] { m })).Single();
    }

    public async Task<TestMaterialDetailDto> GetDetailAsync(int id)
    {
        // 详情允许查看已删除料件(供主清单已删除行的"详情"按钮)
        var entity = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        EnsureCanAccess(entity);
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
        EnsureCanAssignDepartment(request.DepartmentId);
        if (!await _db.TestProjects.AnyAsync(x => x.Id == request.ProjectId && !x.IsDeleted))
            throw new BizException(4046, "测试项目不存在");

        for (var attempt = 0; ; attempt++)
        {
            var m = new TestMaterial
            {
                MaterialNo = await NextMaterialNo(),
                Name = request.Name.Trim(),
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
                return await GetAsync(m.Id);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                _db.Entry(m).State = EntityState.Detached;
            }
        }
    }

    public async Task<TestMaterialDto> UpdateAsync(int id, SaveTestMaterialRequest request)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        EnsureCanAccess(m);
        EnsureCanAssignDepartment(request.DepartmentId);
        if (!await _db.TestProjects.AnyAsync(x => x.Id == request.ProjectId && !x.IsDeleted))
            throw new BizException(4046, "测试项目不存在");

        m.Name = request.Name.Trim();
        m.ProjectId = request.ProjectId;
        m.VendorName = request.VendorName?.Trim();
        m.Model = request.Model?.Trim();
        m.Brand = request.Brand?.Trim();
        m.Quantity = Math.Max(request.Quantity, 1);
        m.DepartmentId = request.DepartmentId;
        m.LocationId = request.LocationId;
        m.CustodianId = request.CustodianId;
        m.ReceivedDate = request.ReceivedDate;
        m.Remark = request.Remark?.Trim();
        if (request.Images is not null) m.ImageUrls = JoinImages(request.Images);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        if (m.IsDeleted) throw new BizException(4048, "测试料件不存在");
        EnsureCanAccess(m);
        if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == id && x.Status == "pending"))
            throw new BizException(4092, "该料件有进行中的流转,不能删除");
        m.IsDeleted = true;
        m.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RestoreAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        EnsureCanAccess(m);
        if (!m.IsDeleted) throw new BizException(4099, "料件未删除,无需恢复");
        m.IsDeleted = false;
        m.DeletedAt = null;
        await _db.SaveChangesAsync();
    }

    public async Task PurgeAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        EnsureCanAccess(m);
        if (!m.IsDeleted) throw new BizException(4097, "请先删除料件后再彻底删除");
        _db.TestMaterials.Remove(m);
        await _db.SaveChangesAsync();
    }

    public async Task<TestMaterialDto> ReturnToVendorAsync(int id)
    {
        var m = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4048, "测试料件不存在");
        if (m.IsDeleted) throw new BizException(4048, "测试料件不存在");
        EnsureCanAccess(m);
        m.Status = MaterialStatus.ReturnedToVendor;
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    // ===== 部门隔离(逻辑比照 AssetService.ApplyQuery) =====
    private IQueryable<TestMaterial> ApplyQuery(IQueryable<TestMaterial> q, TestMaterialQuery query)
    {
        var deleteStatus = query.DeleteStatus?.Trim().ToLowerInvariant();
        q = deleteStatus switch
        {
            "all" => q,
            "deleted" => q.Where(x => x.IsDeleted),
            _ => q.Where(x => !x.IsDeleted)
        };

        var allowed = AllowedDepartmentIds();
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
            var ids = DescendantDepartmentIds(query.DepartmentId.Value);
            q = q.Where(x => x.DepartmentId.HasValue && ids.Contains(x.DepartmentId.Value));
        }
        return q;
    }

    private int[] DescendantDepartmentIds(int rootId)
    {
        var departments = _cache.GetOrCreate(DepartmentTreeCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return _db.Departments.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToList();
        })!;
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

    private int[]? AllowedDepartmentIds()
    {
        var user = _http.HttpContext?.User;
        if (user is null) return null;
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Contains("admin")) return null;
        if (roles.Contains("dept_admin"))
        {
            var deptIdClaim = user.FindFirst("departmentId")?.Value;
            if (int.TryParse(deptIdClaim, out var deptId))
                return DescendantDepartmentIds(deptId);
        }
        return null;
    }

    private void EnsureCanAccess(TestMaterial m)
    {
        var allowed = AllowedDepartmentIds();
        if (allowed != null && (!m.DepartmentId.HasValue || !allowed.Contains(m.DepartmentId.Value)))
            throw new BizException(4047, "无权访问该测试料件");
    }

    private void EnsureCanAssignDepartment(int? departmentId)
    {
        var allowed = AllowedDepartmentIds();
        if (allowed != null && (!departmentId.HasValue || !allowed.Contains(departmentId.Value)))
            throw new BizException(4047, "无权将料件归属到该部门");
    }

    private async Task<string> NextMaterialNo()
    {
        var today = DateTime.Now.Date;
        var prefix = $"TM-{today:yyyyMMdd}-";
        var countToday = await _db.TestMaterials.CountAsync(x => x.MaterialNo.StartsWith(prefix));
        return MaterialNoGenerator.Next(today, countToday);
    }

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

    private static string? JoinImages(IEnumerable<string>? images)
    {
        if (images is null) return null;
        var listed = images.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList();
        if (listed.Count == 0) return null;
        if (listed.Count > 5) throw new BizException(4152, "最多上传 5 张照片");
        return string.Join(',', listed);
    }

    private static List<string> SplitImages(string? imageUrls)
        => string.IsNullOrWhiteSpace(imageUrls)
            ? new List<string>()
            : imageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
