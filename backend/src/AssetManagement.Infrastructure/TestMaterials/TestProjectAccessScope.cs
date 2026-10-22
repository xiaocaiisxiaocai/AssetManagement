using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.TestMaterials;

internal sealed class TestProjectAccessScope
{
    private readonly AppDbContext _db;
    private readonly ClaimsPrincipal? _user;
    private readonly Dictionary<int, int?> _ownerDepartments = new();
    private int[]? _allowedDepartmentIds;

    public TestProjectAccessScope(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _user = http.HttpContext?.User;
    }

    public int? CurrentUserId
    {
        get
        {
            var value = _user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAdmin => _user?.IsInRole("admin") == true ||
        _user?.FindAll(ClaimTypes.Role).Any(x => x.Value == "admin") == true;

    public bool IsSupervisor => _user?.IsInRole("supervisor") == true ||
        _user?.FindAll(ClaimTypes.Role).Any(x => x.Value == "supervisor") == true;

    public bool HasPermission(string permission)
        => _user?.HasClaim("perm", permission) == true;

    public async Task<bool> CanExecuteAsync(
        TestProject project,
        int userId,
        string permission,
        bool ownerMayExecute)
    {
        if (userId <= 0) return false;
        if (IsAdmin) return true;
        if (ownerMayExecute && project.OwnerId == userId) return true;
        if (!HasPermission(permission)) return false;
        return await IsInDepartmentScopeAsync(project.OwnerId, userId);
    }

    public async Task EnsureScopeAsync(TestProject project, int userId)
    {
        if (userId <= 0 || !await IsInDepartmentScopeAsync(project.OwnerId, userId))
            throw new BizException(4031, "无权管理其他部门的测试项目");
    }

    public async Task EnsureCanViewAsync(TestProject project, int userId)
    {
        if (IsAdmin) return;
        if (userId > 0 && await IsInDepartmentScopeAsync(project.OwnerId, userId)) return;
        throw new BizException(4031, "无权查看其他部门的测试项目");
    }

    public async Task<IQueryable<TestProject>> ApplyVisibilityAsync(IQueryable<TestProject> query, int userId)
    {
        if (IsAdmin) return query;
        if (userId <= 0) return query.Where(_ => false);
        var allowed = await AllowedDepartmentIdsAsync(userId);
        if (allowed.Length == 0)
            return query.Where(project => project.OwnerId == userId);
        var ownerIds = await _db.Users.AsNoTracking()
            .Where(user => user.DepartmentId.HasValue && allowed.Contains(user.DepartmentId.Value))
            .Select(user => user.Id)
            .ToArrayAsync();
        return query.Where(project =>
            project.OwnerId == userId ||
            (project.OwnerId.HasValue && ownerIds.Contains(project.OwnerId.Value)));
    }

    public async Task EnsureOwnerAssignableAsync(int ownerId, bool creating)
    {
        var error = await OwnerAssignmentErrorAsync(ownerId, creating);
        if (error is not null)
            throw new BizException(4031, error);
    }

    public async Task<string?> OwnerAssignmentErrorAsync(int? ownerId, bool creating)
    {
        if (IsAdmin || !ownerId.HasValue) return null;
        var userId = CurrentUserId;
        if (!userId.HasValue || userId.Value <= 0)
            return creating && !IsSupervisor
                ? "只能把自己设为项目负责人"
                : "只能指定本部门及下级部门的人员为负责人";

        if (creating && !IsSupervisor && ownerId.Value != userId.Value)
            return "只能把自己设为项目负责人";

        if (await IsInDepartmentScopeAsync(ownerId, userId.Value)) return null;
        return "只能指定本部门及下级部门的人员为负责人";
    }

    public async Task PreloadAsync(IEnumerable<TestProject> projects, int userId)
    {
        if (userId > 0)
            await AllowedDepartmentIdsAsync(userId);
        await PreloadOwnerDepartmentsAsync(
            projects.Where(x => x.OwnerId.HasValue).Select(x => x.OwnerId!.Value));
    }

    public bool IsInDepartmentScope(int? ownerId, int userId)
    {
        if (IsAdmin) return true;
        if (ownerId == userId) return true;
        if (!ownerId.HasValue || userId <= 0) return false;
        var allowed = _allowedDepartmentIds ?? [];
        return _ownerDepartments.TryGetValue(ownerId.Value, out var departmentId) &&
               departmentId.HasValue &&
               allowed.Contains(departmentId.Value);
    }

    private async Task<bool> IsInDepartmentScopeAsync(int? ownerId, int userId)
    {
        if (IsAdmin) return true;
        if (ownerId == userId) return true;
        if (!ownerId.HasValue || userId <= 0) return false;

        var allowed = await AllowedDepartmentIdsAsync(userId);
        if (allowed.Length == 0) return false;
        var ownerDepartmentId = await OwnerDepartmentIdAsync(ownerId.Value);
        return ownerDepartmentId.HasValue && allowed.Contains(ownerDepartmentId.Value);
    }

    private async Task PreloadOwnerDepartmentsAsync(IEnumerable<int> ownerIds)
    {
        var missing = ownerIds.Where(id => !_ownerDepartments.ContainsKey(id)).Distinct().ToArray();
        if (missing.Length == 0) return;

        var rows = await _db.Users.AsNoTracking()
            .Where(x => missing.Contains(x.Id))
            .Select(x => new { x.Id, x.DepartmentId })
            .ToListAsync();
        foreach (var id in missing)
            _ownerDepartments[id] = null;
        foreach (var row in rows)
            _ownerDepartments[row.Id] = row.DepartmentId;
    }

    private async Task<int?> OwnerDepartmentIdAsync(int ownerId)
    {
        if (_ownerDepartments.TryGetValue(ownerId, out var cached)) return cached;
        var departmentId = await _db.Users.AsNoTracking()
            .Where(x => x.Id == ownerId)
            .Select(x => x.DepartmentId)
            .SingleOrDefaultAsync();
        _ownerDepartments[ownerId] = departmentId;
        return departmentId;
    }

    private async Task<int[]> AllowedDepartmentIdsAsync(int userId)
    {
        if (_allowedDepartmentIds is not null) return _allowedDepartmentIds;
        var departmentId = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.DepartmentId)
            .SingleOrDefaultAsync();
        _allowedDepartmentIds = departmentId.HasValue
            ? await DescendantDepartmentIdsAsync(departmentId.Value)
            : [];
        return _allowedDepartmentIds;
    }

    private async Task<int[]> DescendantDepartmentIdsAsync(int rootId)
    {
        var departments = await _db.Departments.AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync();
        var ids = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        while (queue.TryDequeue(out var parentId))
        {
            foreach (var child in departments.Where(x => x.ParentId == parentId))
            {
                if (ids.Add(child.Id)) queue.Enqueue(child.Id);
            }
        }
        return ids.ToArray();
    }
}
