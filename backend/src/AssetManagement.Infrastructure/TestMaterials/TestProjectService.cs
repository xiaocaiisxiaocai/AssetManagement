using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.TestMaterials;

public class TestProjectService : ITestProjectService
{
    private readonly AppDbContext _db;

    public TestProjectService(AppDbContext db) => _db = db;

    public async Task<List<TestProjectDto>> ListAsync(string? deleteStatus)
    {
        var status = deleteStatus?.Trim().ToLowerInvariant();
        IQueryable<TestProject> q = _db.TestProjects;
        q = status switch
        {
            "all" => q,
            "deleted" => q.Where(x => x.IsDeleted),
            _ => q.Where(x => !x.IsDeleted)
        };
        var projects = await q.OrderByDescending(x => x.Id).ToListAsync();
        var ids = projects.Select(x => x.Id).ToArray();
        var counts = await _db.TestMaterials
            .Where(x => !x.IsDeleted && ids.Contains(x.ProjectId))
            .GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        return projects.Select(x => ToDto(x, counts.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<TestProjectDto> CreateAsync(SaveTestProjectRequest request)
    {
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        var project = new TestProject
        {
            Name = name,
            Code = request.Code?.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.TestProjects.Add(project);
        await _db.SaveChangesAsync();
        return ToDto(project, 0);
    }

    public async Task<TestProjectDto> UpdateAsync(int id, SaveTestProjectRequest request)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        project.Name = name;
        project.Code = request.Code?.Trim();
        project.Description = request.Description?.Trim();
        await _db.SaveChangesAsync();
        var count = await _db.TestMaterials.CountAsync(x => !x.IsDeleted && x.ProjectId == id);
        return ToDto(project, count);
    }

    public async Task DeleteAsync(int id)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        if (await _db.TestMaterials.AnyAsync(x => !x.IsDeleted && x.ProjectId == id))
            throw new BizException(4092, "该项目下仍有测试料件,不能删除");
        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RestoreAsync(int id)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        if (!project.IsDeleted) throw new BizException(4099, "项目未删除,无需恢复");
        project.IsDeleted = false;
        project.DeletedAt = null;
        await _db.SaveChangesAsync();
    }

    public async Task PurgeAsync(int id)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        if (!project.IsDeleted) throw new BizException(4097, "请先删除项目后再彻底删除");
        if (await _db.TestMaterials.AnyAsync(x => x.ProjectId == id))
            throw new BizException(4092, "该项目下仍有测试料件,不能彻底删除");
        _db.TestProjects.Remove(project);
        await _db.SaveChangesAsync();
    }

    private static TestProjectDto ToDto(TestProject x, int materialCount) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Code = x.Code,
        Description = x.Description,
        CreatedAt = x.CreatedAt,
        IsDeleted = x.IsDeleted,
        DeletedAt = x.DeletedAt,
        MaterialCount = materialCount
    };
}
