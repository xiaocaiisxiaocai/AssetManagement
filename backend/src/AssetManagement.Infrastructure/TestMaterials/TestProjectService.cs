using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.TestMaterials;

public class TestProjectService : ITestProjectService
{
    public const string OptionKindProjectType = "project_type";
    public const string OptionKindProgress = "project_progress";

    private readonly AppDbContext _db;

    public TestProjectService(AppDbContext db) => _db = db;

    public async Task<List<TestProjectDto>> ListAsync(string? deleteStatus, int currentUserId)
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
        return await ToDtos(projects, counts, currentUserId);
    }

    public async Task<TestProjectDto> CreateAsync(SaveTestProjectRequest request)
    {
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        await ValidateProjectReferences(request);
        var project = new TestProject
        {
            Name = name,
            Code = request.Code?.Trim(),
            ProjectTypeCode = NormalizeOptional(request.ProjectTypeCode),
            StartDate = request.StartDate?.Date,
            PlannedFinishDate = request.PlannedFinishDate?.Date,
            ClosedDate = request.ClosedDate?.Date,
            ProgressCode = NormalizeOptional(request.ProgressCode),
            OwnerId = request.OwnerId,
            TestStatus = request.TestStatus?.Trim(),
            FollowUpIntervalDays = NormalizeInterval(request.FollowUpIntervalDays),
            CreatedAt = DateTime.UtcNow
        };
        _db.TestProjects.Add(project);
        await _db.SaveChangesAsync();
        return (await ToDtos(new[] { project }, new Dictionary<int, int>(), null)).Single();
    }

    public async Task<TestProjectDto> UpdateAsync(int id, SaveTestProjectRequest request)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        await ValidateProjectReferences(request);
        project.Name = name;
        project.Code = request.Code?.Trim();
        project.ProjectTypeCode = NormalizeOptional(request.ProjectTypeCode);
        project.StartDate = request.StartDate?.Date;
        project.PlannedFinishDate = request.PlannedFinishDate?.Date;
        project.ClosedDate = request.ClosedDate?.Date;
        project.ProgressCode = NormalizeOptional(request.ProgressCode);
        project.OwnerId = request.OwnerId;
        project.TestStatus = request.TestStatus?.Trim();
        project.FollowUpIntervalDays = NormalizeInterval(request.FollowUpIntervalDays);
        await _db.SaveChangesAsync();
        var count = await _db.TestMaterials.CountAsync(x => !x.IsDeleted && x.ProjectId == id);
        return (await ToDtos(new[] { project }, new Dictionary<int, int> { [id] = count }, null)).Single();
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

    public async Task<List<TestProjectOptionDto>> ListOptionsAsync(string? kind)
    {
        var q = _db.TestProjectOptions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind))
        {
            q = q.Where(x => x.Kind == kind.Trim());
        }

        return await q.OrderBy(x => x.Kind).ThenBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => ToOptionDto(x))
            .ToListAsync();
    }

    public async Task<TestProjectOptionDto> CreateOptionAsync(SaveTestProjectOptionRequest request)
    {
        ValidateOptionRequest(request);
        var option = new TestProjectOption
        {
            Kind = request.Kind.Trim(),
            Code = request.Code.Trim(),
            Label = request.Label.Trim(),
            Sort = request.Sort,
            IsActive = request.IsActive
        };
        _db.TestProjectOptions.Add(option);
        await _db.SaveChangesAsync();
        return ToOptionDto(option);
    }

    public async Task<TestProjectOptionDto> UpdateOptionAsync(int id, SaveTestProjectOptionRequest request)
    {
        ValidateOptionRequest(request);
        var option = await _db.TestProjectOptions.FindAsync(id)
            ?? throw new BizException(4046, "项目配置项不存在");
        option.Kind = request.Kind.Trim();
        option.Code = request.Code.Trim();
        option.Label = request.Label.Trim();
        option.Sort = request.Sort;
        option.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return ToOptionDto(option);
    }

    public async Task DeleteOptionAsync(int id)
    {
        var option = await _db.TestProjectOptions.FindAsync(id)
            ?? throw new BizException(4046, "项目配置项不存在");
        _db.TestProjectOptions.Remove(option);
        await _db.SaveChangesAsync();
    }

    public async Task<List<TestProjectFollowupDto>> ListFollowupsAsync(int projectId)
    {
        await EnsureProjectExists(projectId);
        var followups = await _db.TestProjectFollowups
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return await ToFollowupDtos(followups);
    }

    public async Task<TestProjectFollowupDto> CreateFollowupAsync(int projectId, SaveTestProjectFollowupRequest request, int currentUserId)
    {
        var project = await LoadProject(projectId);
        await EnsureCanWriteFollowup(project, currentUserId);
        var content = (request.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content)) throw new BizException(4001, "请填写落地情况");
        var latest = await LatestFollowup(projectId);
        var followup = new TestProjectFollowup
        {
            ProjectId = projectId,
            DueDate = request.DueDate?.Date ?? NextFollowUpDueDate(project, latest),
            Content = content,
            FilledById = currentUserId,
            FilledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _db.TestProjectFollowups.Add(followup);
        await _db.SaveChangesAsync();
        return (await ToFollowupDtos(new[] { followup })).Single();
    }

    public async Task<TestProjectFollowupDto> UpdateFollowupAsync(int projectId, int followupId, SaveTestProjectFollowupRequest request, int currentUserId)
    {
        var project = await LoadProject(projectId);
        await EnsureCanWriteFollowup(project, currentUserId);
        var followup = await _db.TestProjectFollowups.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == followupId && x.ProjectId == projectId)
            ?? throw new BizException(4046, "跟进记录不存在");
        var content = (request.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content)) throw new BizException(4001, "请填写落地情况");
        followup.DueDate = request.DueDate?.Date ?? followup.DueDate;
        followup.Content = content;
        followup.FilledById = currentUserId;
        followup.FilledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ToFollowupDtos(new[] { followup })).Single();
    }

    private async Task<List<TestProjectDto>> ToDtos(IEnumerable<TestProject> projects, Dictionary<int, int> counts, int? currentUserId)
    {
        var list = projects.ToList();
        var ownerIds = list.Where(x => x.OwnerId.HasValue).Select(x => x.OwnerId!.Value).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var options = await _db.TestProjectOptions.AsNoTracking().ToListAsync();
        var optionMap = options.ToDictionary(x => (x.Kind, x.Code), x => x.Label);
        var projectIds = list.Select(x => x.Id).ToArray();
        var followups = await _db.TestProjectFollowups.AsNoTracking()
            .Where(x => projectIds.Contains(x.ProjectId))
            .OrderByDescending(x => x.FilledAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var latestByProject = followups
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.First());
        var isAdmin = currentUserId.HasValue && await IsAdmin(currentUserId.Value);

        return list.Select(x =>
        {
            latestByProject.TryGetValue(x.Id, out var latest);
            var nextDue = NextFollowUpDueDate(x, latest);
            return new TestProjectDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ProjectTypeCode = x.ProjectTypeCode,
                ProjectTypeLabel = LabelFor(optionMap, OptionKindProjectType, x.ProjectTypeCode),
                StartDate = x.StartDate,
                PlannedFinishDate = x.PlannedFinishDate,
                ClosedDate = x.ClosedDate,
                ProgressCode = x.ProgressCode,
                ProgressLabel = LabelFor(optionMap, OptionKindProgress, x.ProgressCode),
                OwnerId = x.OwnerId,
                OwnerName = x.OwnerId.HasValue ? users.GetValueOrDefault(x.OwnerId.Value) : null,
                TestStatus = x.TestStatus,
                FollowUpIntervalDays = NormalizeInterval(x.FollowUpIntervalDays),
                NextFollowUpDueDate = nextDue,
                FollowUpStatus = FollowUpStatus(nextDue),
                LatestFollowUpContent = latest?.Content,
                LatestFollowUpAt = latest?.FilledAt,
                CanWriteFollowUp = currentUserId.HasValue && (isAdmin || x.OwnerId == currentUserId.Value),
                CreatedAt = x.CreatedAt,
                IsDeleted = x.IsDeleted,
                DeletedAt = x.DeletedAt,
                MaterialCount = counts.GetValueOrDefault(x.Id)
            };
        }).ToList();
    }

    private async Task<List<TestProjectFollowupDto>> ToFollowupDtos(IEnumerable<TestProjectFollowup> followups)
    {
        var list = followups.ToList();
        var userIds = list.Select(x => x.FilledById).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        return list.Select(x => new TestProjectFollowupDto
        {
            Id = x.Id,
            ProjectId = x.ProjectId,
            DueDate = x.DueDate,
            Content = x.Content,
            FilledById = x.FilledById,
            FilledByName = users.GetValueOrDefault(x.FilledById),
            FilledAt = x.FilledAt
        }).ToList();
    }

    private async Task ValidateProjectReferences(SaveTestProjectRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ProjectTypeCode) &&
            !await _db.TestProjectOptions.AnyAsync(x => x.Kind == OptionKindProjectType && x.Code == request.ProjectTypeCode && x.IsActive))
            throw new BizException(4002, "项目类型不存在或已停用");
        if (!string.IsNullOrWhiteSpace(request.ProgressCode) &&
            !await _db.TestProjectOptions.AnyAsync(x => x.Kind == OptionKindProgress && x.Code == request.ProgressCode && x.IsActive))
            throw new BizException(4002, "项目进度不存在或已停用");
        if (request.OwnerId.HasValue &&
            !await _db.Users.AnyAsync(x => x.Id == request.OwnerId.Value && x.IsActive))
            throw new BizException(4041, "负责人不存在或已停用");
    }

    private async Task<TestProject> LoadProject(int projectId)
        => await _db.TestProjects.SingleOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted)
           ?? throw new BizException(4046, "测试项目不存在");

    private async Task EnsureProjectExists(int projectId)
        => _ = await LoadProject(projectId);

    private async Task EnsureCanWriteFollowup(TestProject project, int currentUserId)
    {
        if (project.OwnerId == currentUserId || await IsAdmin(currentUserId)) return;
        throw new BizException(4031, "只有项目负责人或管理员可以填写落地跟进");
    }

    private async Task<bool> IsAdmin(int userId)
        => await _db.UserRoles
            .Include(x => x.Role)
            .AnyAsync(x => x.UserId == userId && x.Role != null && x.Role.Code == "admin");

    private async Task<TestProjectFollowup?> LatestFollowup(int projectId)
        => await _db.TestProjectFollowups
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.FilledAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

    private static DateTime NextFollowUpDueDate(TestProject project, TestProjectFollowup? latest)
    {
        var baseDate = latest?.FilledAt.Date
            ?? project.StartDate?.Date
            ?? project.CreatedAt.Date;
        return baseDate.AddDays(NormalizeInterval(project.FollowUpIntervalDays));
    }

    private static string FollowUpStatus(DateTime dueDate)
    {
        var today = DateTime.UtcNow.Date;
        if (dueDate.Date < today) return "overdue";
        if (dueDate.Date == today) return "due";
        return "upcoming";
    }

    private static string? LabelFor(Dictionary<(string Kind, string Code), string> optionMap, string kind, string? code)
        => string.IsNullOrWhiteSpace(code) ? null : optionMap.GetValueOrDefault((kind, code));

    private static TestProjectOptionDto ToOptionDto(TestProjectOption x) => new()
    {
        Id = x.Id,
        Kind = x.Kind,
        Code = x.Code,
        Label = x.Label,
        Sort = x.Sort,
        IsActive = x.IsActive
    };

    private static void ValidateOptionRequest(SaveTestProjectOptionRequest request)
    {
        if (request.Kind is not OptionKindProjectType and not OptionKindProgress)
            throw new BizException(4001, "配置类型不正确");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BizException(4001, "配置编码不能为空");
        if (string.IsNullOrWhiteSpace(request.Label)) throw new BizException(4001, "配置名称不能为空");
    }

    private static string? NormalizeOptional(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static int NormalizeInterval(int days)
        => Math.Clamp(days <= 0 ? 14 : days, 1, 365);
}
