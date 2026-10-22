using System.Security.Claims;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.TestMaterials;

internal sealed class TestProjectAccessScope
{
    private readonly AppDbContext _db;
    private readonly ClaimsPrincipal? _user;

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
        return await IsInSupervisorScopeAsync(project, userId);
    }

    public async Task EnsureScopeAsync(TestProject project, int userId)
    {
        if (userId <= 0 || !await IsInSupervisorScopeAsync(project, userId))
            throw new AssetManagement.Application.Common.BizException(4031, "无权管理其他部门的测试项目");
    }

    public async Task<IQueryable<TestProject>> ApplySupervisorScopeAsync(
        IQueryable<TestProject> query,
        int userId)
    {
        if (!IsSupervisor || IsAdmin || userId <= 0) return query;

        var departmentId = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.DepartmentId)
            .SingleOrDefaultAsync();
        if (!departmentId.HasValue) return query.Where(_ => false);

        var allowed = await DescendantDepartmentIdsAsync(departmentId.Value);
        var ownerIds = await _db.Users.AsNoTracking()
            .Where(x => x.DepartmentId.HasValue && allowed.Contains(x.DepartmentId.Value))
            .Select(x => x.Id)
            .ToArrayAsync();
        return query.Where(x => x.OwnerId.HasValue && ownerIds.Contains(x.OwnerId.Value));
    }

    private async Task<bool> IsInSupervisorScopeAsync(TestProject project, int userId)
    {
        if (IsAdmin || !IsSupervisor) return true;
        if (project.OwnerId == userId) return true;
        if (!project.OwnerId.HasValue) return false;

        var supervisorDepartmentId = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.DepartmentId)
            .SingleOrDefaultAsync();
        var ownerDepartmentId = await _db.Users.AsNoTracking()
            .Where(x => x.Id == project.OwnerId.Value)
            .Select(x => x.DepartmentId)
            .SingleOrDefaultAsync();
        if (!supervisorDepartmentId.HasValue || !ownerDepartmentId.HasValue)
            return false;

        var allowed = await DescendantDepartmentIdsAsync(supervisorDepartmentId.Value);
        return allowed.Contains(ownerDepartmentId.Value);
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
