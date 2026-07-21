using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AssetManagement.Infrastructure.TestMaterials;

public class TestProjectService : ITestProjectService
{
    public const string OptionKindProjectType = "project_type";
    public const string OptionKindProgress = "project_progress";
    public const string ProgressLanding = "landing";
    public const string ProgressClosed = "closed";
    private static readonly HashSet<string> ReservedProgressCodes =
        new(StringComparer.OrdinalIgnoreCase) { ProgressLanding, ProgressClosed };

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
        // 兼容旧客户端的无分页端点，但必须有硬上限；正式列表使用 ListPageAsync。
        var projects = await q.OrderByDescending(x => x.Id)
            .Take(AppConstants.MaxPageSize)
            .ToListAsync();
        var ids = projects.Select(x => x.Id).ToArray();
        var counts = await _db.TestMaterials
            .Where(x => !x.IsDeleted && ids.Contains(x.ProjectId))
            .GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        return await ToDtos(projects, counts, currentUserId);
    }

    public async Task<PagedResult<TestProjectDto>> ListPageAsync(TestProjectPageQuery query, int currentUserId)
    {
        var (page, pageSize) = Pagination.Normalize(query.Page, query.PageSize);
        var status = query.DeleteStatus?.Trim().ToLowerInvariant();
        IQueryable<TestProject> projectsQuery = _db.TestProjects.AsNoTracking();
        projectsQuery = status switch
        {
            "all" => projectsQuery,
            "deleted" => projectsQuery.Where(x => x.IsDeleted),
            _ => projectsQuery.Where(x => !x.IsDeleted)
        };
        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            var code = query.Code.Trim();
            projectsQuery = projectsQuery.Where(x => x.Code != null && x.Code.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim();
            projectsQuery = projectsQuery.Where(x => x.Name.Contains(name));
        }
        if (query.OwnerId.HasValue)
            projectsQuery = projectsQuery.Where(x => x.OwnerId == query.OwnerId.Value);
        if (!string.IsNullOrWhiteSpace(query.ProgressCode))
        {
            var progressCode = query.ProgressCode.Trim();
            projectsQuery = projectsQuery.Where(x => x.ProgressCode == progressCode);
        }
        if (!string.IsNullOrWhiteSpace(query.ProjectTypeCode))
        {
            var projectTypeCode = query.ProjectTypeCode.Trim();
            projectsQuery = projectsQuery.Where(x => x.ProjectTypeCode == projectTypeCode);
        }

        var total = await projectsQuery.CountAsync();
        var offset = Pagination.GetOffset(page, pageSize, total);
        var projects = offset.HasValue
            ? await projectsQuery.OrderByDescending(x => x.Id)
                .Skip(offset.Value)
                .Take(pageSize)
                .ToListAsync()
            : [];
        var ids = projects.Select(x => x.Id).ToArray();
        var counts = await _db.TestMaterials.AsNoTracking()
            .Where(x => !x.IsDeleted && ids.Contains(x.ProjectId))
            .GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        return new PagedResult<TestProjectDto>
        {
            Items = await ToDtos(projects, counts, currentUserId),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<TestProjectDto> CreateAsync(SaveTestProjectRequest request)
    {
        NormalizeProjectCodes(request);
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        var code = (request.Code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) throw new BizException(4001, "项目编号不能为空");
        ValidateProjectTextLengths(name, code, request);
        ValidateProjectRequiredFields(request);
        ValidateProjectTimeline(request);
        await EnsureProjectUnique(code, name);
        await ValidateProjectReferences(request);
        var project = new TestProject
        {
            Name = name,
            Code = code,
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
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "项目编号或项目名称已存在");
        }
        return (await ToDtos(new[] { project }, new Dictionary<int, int>(), null)).Single();
    }

    public async Task<TestProjectDto> UpdateAsync(int id, SaveTestProjectRequest request)
    {
        NormalizeProjectCodes(request);
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        var code = (request.Code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) throw new BizException(4001, "项目编号不能为空");
        ValidateProjectTextLengths(name, code, request);
        ValidateProjectRequiredFields(request);
        ValidateProjectTimeline(request);
        await EnsureProjectUnique(code, name, id);
        await ValidateProjectReferences(request);
        project.Name = name;
        project.Code = code;
        project.ProjectTypeCode = NormalizeOptional(request.ProjectTypeCode);
        project.StartDate = request.StartDate?.Date;
        project.PlannedFinishDate = request.PlannedFinishDate?.Date;
        project.ClosedDate = request.ClosedDate?.Date;
        project.ProgressCode = NormalizeOptional(request.ProgressCode);
        project.OwnerId = request.OwnerId;
        project.TestStatus = request.TestStatus?.Trim();
        project.FollowUpIntervalDays = NormalizeInterval(request.FollowUpIntervalDays);
        project.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该项目已被他人修改，请刷新后重试");
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "项目编号或项目名称已存在");
        }
        var count = await _db.TestMaterials.CountAsync(x => !x.IsDeleted && x.ProjectId == id);
        return (await ToDtos(new[] { project }, new Dictionary<int, int> { [id] = count }, null)).Single();
    }

    public async Task<TestProjectDto> UpdateProgressAsync(
        int id,
        UpdateTestProjectProgressRequest request,
        int currentUserId)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        if (project.OwnerId != currentUserId && !await IsAdmin(currentUserId))
            throw new BizException(4031, "只有项目负责人或管理员可以更新项目进展");

        var progressCode = request.ProgressCode?.Trim();
        if (string.IsNullOrWhiteSpace(progressCode))
            throw new BizException(4001, "项目进度不能为空");
        if (!await _db.TestProjectOptions.AnyAsync(x =>
                x.Kind == OptionKindProgress && x.Code == progressCode && x.IsActive))
            throw new BizException(4002, "项目进度不存在或已停用");

        var closedDate = request.ClosedDate?.Date;
        ValidateProjectProgressTimeline(project.StartDate, progressCode, closedDate);
        var testStatus = request.TestStatus?.Trim();
        if (testStatus?.Length > 1000)
            throw new BizException(4001, "测试情况不能超过1000个字符");

        project.ProgressCode = progressCode;
        project.ClosedDate = closedDate;
        project.TestStatus = testStatus;
        project.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该项目已被他人修改，请刷新后重试");
        }

        var count = await _db.TestMaterials.CountAsync(x => !x.IsDeleted && x.ProjectId == id);
        return (await ToDtos(
            new[] { project },
            new Dictionary<int, int> { [id] = count },
            currentUserId)).Single();
    }

    public async Task DeleteAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        if (await _db.TestMaterials.AnyAsync(x => !x.IsDeleted && x.ProjectId == id))
            throw new BizException(4092, "该项目下仍有测试料件,不能删除");
        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;
        project.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该项目已被他人修改，请刷新后重试");
        }
        await transaction.CommitAsync();
    }

    public async Task RestoreAsync(int id)
    {
        var project = await _db.TestProjects.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "测试项目不存在");
        if (!project.IsDeleted) throw new BizException(4099, "项目未删除,无需恢复");
        project.IsDeleted = false;
        project.DeletedAt = null;
        project.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该项目已被他人修改，请刷新后重试");
        }
    }

    public async Task PurgeAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4046, "测试项目不存在");
        if (!project.IsDeleted) throw new BizException(4097, "请先删除项目后再彻底删除");
        if (await _db.TestMaterials.AnyAsync(x => x.ProjectId == id))
            throw new BizException(4092, "该项目下仍有测试料件(含已删除),不能彻底删除");
        if (await _db.TestProjectFollowups.AnyAsync(x => x.ProjectId == id))
            throw new BizException(4092, "该项目仍有跟进历史,不能彻底删除");
        _db.TestProjects.Remove(project);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该项目已被他人修改，请刷新后重试");
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            throw new BizException(4092, "该项目已被其他数据使用，不能彻底删除");
        }
        await transaction.CommitAsync();
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
        var kind = request.Kind.Trim();
        var code = request.Code.Trim().ToLowerInvariant();
        await EnsureOptionCodeAvailable(kind, code);
        var option = new TestProjectOption
        {
            Kind = kind,
            Code = code,
            Label = request.Label.Trim(),
            Sort = request.Sort,
            IsActive = request.IsActive
        };
        _db.TestProjectOptions.Add(option);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "同类项目配置编码已存在");
        }
        return ToOptionDto(option);
    }

    public async Task<TestProjectOptionDto> UpdateOptionAsync(int id, SaveTestProjectOptionRequest request)
    {
        ValidateOptionRequest(request);
        var option = await _db.TestProjectOptions.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "项目配置项不存在");
        var kind = request.Kind.Trim();
        var code = request.Code.Trim().ToLowerInvariant();
        await EnsureOptionCodeAvailable(kind, code, id);
        await EnsureOptionCanChangeAsync(option, kind, code, request.IsActive);
        option.Kind = kind;
        option.Code = code;
        option.Label = request.Label.Trim();
        option.Sort = request.Sort;
        option.IsActive = request.IsActive;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "同类项目配置编码已存在");
        }
        return ToOptionDto(option);
    }

    public async Task DeleteOptionAsync(int id)
    {
        var option = await _db.TestProjectOptions.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4046, "项目配置项不存在");
        if (option.Kind == OptionKindProgress && ReservedProgressCodes.Contains(option.Code))
        {
            throw new BizException(4094, "落地跟进和已结案是系统保留进度，不能删除");
        }
        if (option.Kind == OptionKindProjectType &&
            await _db.TestProjects.AnyAsync(x => x.ProjectTypeCode == option.Code))
        {
            throw new BizException(4094, "配置项已被项目使用，不能删除");
        }
        if (option.Kind == OptionKindProgress &&
            await _db.TestProjects.AnyAsync(x => x.ProgressCode == option.Code))
        {
            throw new BizException(4094, "配置项已被项目使用，不能删除");
        }
        _db.TestProjectOptions.Remove(option);
        await _db.SaveChangesAsync();
    }

    public async Task<List<TestProjectFollowupDto>> ListFollowupsAsync(int projectId)
    {
        await EnsureProjectExists(projectId, includeDeleted: true);
        var followups = await _db.TestProjectFollowups
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return await ToFollowupDtos(followups);
    }

    public async Task<TestProjectFollowupDto> CreateFollowupAsync(int projectId, SaveTestProjectFollowupRequest request, int currentUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await LockActiveProjectAsync(projectId);
        await EnsureCanWriteFollowup(project, currentUserId);
        var content = (request.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content)) throw new BizException(4001, "请填写落地情况");
        EnsureMaxLength(content, 2000, "落地情况");
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
        var result = (await ToFollowupDtos(new[] { followup })).Single();
        await transaction.CommitAsync();
        return result;
    }

    public async Task<TestProjectFollowupDto> UpdateFollowupAsync(int projectId, int followupId, SaveTestProjectFollowupRequest request, int currentUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await LockActiveProjectAsync(projectId);
        await EnsureCanWriteFollowup(project, currentUserId);
        var followup = await _db.TestProjectFollowups.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == followupId && x.ProjectId == projectId)
            ?? throw new BizException(4046, "跟进记录不存在");
        var content = (request.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content)) throw new BizException(4001, "请填写落地情况");
        EnsureMaxLength(content, 2000, "落地情况");
        followup.DueDate = request.DueDate?.Date ?? followup.DueDate;
        followup.Content = content;
        await _db.SaveChangesAsync();
        var result = (await ToFollowupDtos(new[] { followup })).Single();
        await transaction.CommitAsync();
        return result;
    }

    public async Task DeleteFollowupAsync(int projectId, int followupId, int currentUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await LockActiveProjectAsync(projectId);
        await EnsureCanWriteFollowup(project, currentUserId);
        var followup = await _db.TestProjectFollowups.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == followupId && x.ProjectId == projectId)
            ?? throw new BizException(4046, "跟进记录不存在");
        _db.TestProjectFollowups.Remove(followup);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<TestProjectStatsDto> GetStatsAsync()
    {
        var year = BusinessClock.Now.Year;
        var projects = await _db.TestProjects
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync();

        var typeOptions = await _db.TestProjectOptions.AsNoTracking()
            .Where(x => x.Kind == OptionKindProjectType && x.IsActive)
            .ToDictionaryAsync(x => x.Code, x => x.Label);

        int closed = projects.Count(x => string.Equals(x.ProgressCode, ProgressClosed, StringComparison.OrdinalIgnoreCase));
        int landed = projects.Count(x => string.Equals(x.ProgressCode, ProgressLanding, StringComparison.OrdinalIgnoreCase));
        // 进度分布必须互斥：这里表示尚未进入“落地跟进”或“结案”的规划/测试阶段。
        int inProgress = projects.Count - closed - landed;

        var projectIds = projects.Select(x => x.Id).ToArray();
        var yearStartUtc = BusinessClock.ToUtc(new DateTime(year, 1, 1));
        var nextYearStartUtc = BusinessClock.ToUtc(new DateTime(year + 1, 1, 1));
        var followups = await _db.TestProjectFollowups
            .AsNoTracking()
            .Where(x => projectIds.Contains(x.ProjectId) &&
                        x.FilledAt >= yearStartUtc && x.FilledAt < nextYearStartUtc)
            .Select(x => x.FilledAt)
            .ToListAsync();
        var followupBusinessTimes = followups.Select(BusinessClock.FromUtc).ToList();

        var typeDist = projects
            .Where(x => !string.IsNullOrEmpty(x.ProjectTypeCode))
            .GroupBy(x => x.ProjectTypeCode!)
            .Select(g => new TypeDistItem
            {
                Label = typeOptions.GetValueOrDefault(g.Key, g.Key),
                Count = g.Count()
            })
            .ToList();
        var unknownCount = projects.Count(x => string.IsNullOrEmpty(x.ProjectTypeCode));
        if (unknownCount > 0)
            typeDist.Add(new TypeDistItem { Label = "未分类", Count = unknownCount });

        // 当年各月：结案项目数与实际填写的落地跟进记录数。
        var monthlyStat = Enumerable.Range(1, 12).Select(m => new MonthlyStatItem
        {
            Month = m,
            ClosedCount = projects.Count(x => x.ClosedDate?.Year == year && x.ClosedDate?.Month == m),
            FollowUpCount = followupBusinessTimes.Count(x => x.Month == m)
        }).ToList();

        return new TestProjectStatsDto
        {
            Total = projects.Count,
            Closed = closed,
            InProgress = inProgress,
            Landed = landed,
            TypeDist = typeDist,
            MonthlyStat = monthlyStat
        };
    }

    private async Task<List<TestProjectDto>> ToDtos(IEnumerable<TestProject> projects, Dictionary<int, int> counts, int? currentUserId)
    {
        var list = projects.ToList();
        var ownerIds = list.Where(x => x.OwnerId.HasValue).Select(x => x.OwnerId!.Value).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var options = await _db.TestProjectOptions.AsNoTracking().ToListAsync();
        var optionMap = options
            .GroupBy(x => (x.Kind, Code: x.Code.ToLowerInvariant()))
            .ToDictionary(x => x.Key, x => x.First().Label);
        var projectIds = list.Select(x => x.Id).ToArray();
        var followups = await _db.TestProjectFollowups.AsNoTracking()
            .Where(x => projectIds.Contains(x.ProjectId))
            .OrderByDescending(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var latestByProject = followups
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.First());
        var isAdmin = currentUserId.HasValue && await IsAdmin(currentUserId.Value);

        return list.Select(x =>
        {
            latestByProject.TryGetValue(x.Id, out var latest);
            var isLanding = IsLandingProgress(x);
            DateTime? nextDue = isLanding ? NextFollowUpDueDate(x, latest) : null;
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
                FollowUpStatus = string.Equals(x.ProgressCode, ProgressClosed, StringComparison.OrdinalIgnoreCase)
                    ? "closed"
                    : FollowUpStatus(nextDue),
                LatestFollowUpContent = latest?.Content,
                LatestFollowUpAt = latest?.FilledAt,
                CanWriteFollowUp = !x.IsDeleted && isLanding && currentUserId.HasValue && (isAdmin || x.OwnerId == currentUserId.Value),
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

    private static void ValidateProjectRequiredFields(SaveTestProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectTypeCode)) throw new BizException(4001, "项目类型不能为空");
        if (string.IsNullOrWhiteSpace(request.ProgressCode)) throw new BizException(4001, "项目进度不能为空");
        if (!request.OwnerId.HasValue) throw new BizException(4001, "负责人不能为空");
        if (!request.StartDate.HasValue) throw new BizException(4001, "开始时间不能为空");
        if (!request.PlannedFinishDate.HasValue) throw new BizException(4001, "计划完成时间不能为空");
        if (request.FollowUpIntervalDays < 1) throw new BizException(4001, "跟进间隔必须大于 0");
    }

    private static void ValidateProjectTimeline(SaveTestProjectRequest request)
    {
        var startDate = request.StartDate!.Value.Date;
        var plannedFinishDate = request.PlannedFinishDate!.Value.Date;
        var closedDate = request.ClosedDate?.Date;
        var isClosed = string.Equals(request.ProgressCode?.Trim(), ProgressClosed, StringComparison.OrdinalIgnoreCase);

        if (plannedFinishDate < startDate)
            throw new BizException(4001, "计划完成时间不能早于开始时间");
        if (closedDate.HasValue && closedDate.Value < startDate)
            throw new BizException(4001, "结案时间不能早于开始时间");
        if (isClosed && !closedDate.HasValue)
            throw new BizException(4001, "已结案项目必须填写结案时间");
        if (!isClosed && closedDate.HasValue)
            throw new BizException(4001, "只有已结案项目才能填写结案时间");
    }

    private static void ValidateProjectProgressTimeline(
        DateTime? startDate,
        string progressCode,
        DateTime? closedDate)
    {
        var isClosed = string.Equals(progressCode, ProgressClosed, StringComparison.OrdinalIgnoreCase);
        if (startDate.HasValue && closedDate.HasValue && closedDate.Value < startDate.Value.Date)
            throw new BizException(4001, "结案时间不能早于开始时间");
        if (isClosed && !closedDate.HasValue)
            throw new BizException(4001, "已结案项目必须填写结案时间");
        if (!isClosed && closedDate.HasValue)
            throw new BizException(4001, "只有已结案项目才能填写结案时间");
    }

    private async Task<TestProject> LoadProject(int projectId)
        => await _db.TestProjects.SingleOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted)
           ?? throw new BizException(4046, "测试项目不存在");

    private async Task<TestProject> LockActiveProjectAsync(int projectId)
    {
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {projectId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync();
        if (project is null || project.IsDeleted)
            throw new BizException(4046, "测试项目不存在");
        return project;
    }

    private async Task EnsureProjectExists(int projectId, bool includeDeleted = false)
    {
        if (!await _db.TestProjects.AnyAsync(x => x.Id == projectId && (includeDeleted || !x.IsDeleted)))
            throw new BizException(4046, "测试项目不存在");
    }

    private async Task EnsureCanWriteFollowup(TestProject project, int currentUserId)
    {
        // 落地跟进不是普通项目编辑权限：只有进入落地阶段后，项目负责人或管理员才能填写。
        if (!IsLandingProgress(project))
            throw new BizException(4031, "项目进入落地跟进后才能填写落地跟进");
        if (project.OwnerId == currentUserId || await IsAdmin(currentUserId)) return;
        throw new BizException(4031, "只有项目负责人或管理员可以填写落地跟进");
    }

    private static bool IsLandingProgress(TestProject project)
        => string.Equals(project.ProgressCode, ProgressLanding, StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsAdmin(int userId)
        => await _db.UserRoles
            .Include(x => x.Role)
            .AnyAsync(x => x.UserId == userId && x.Role != null && x.Role.Code == "admin");

    private async Task<TestProjectFollowup?> LatestFollowup(int projectId)
        => await _db.TestProjectFollowups
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

    private static DateTime NextFollowUpDueDate(TestProject project, TestProjectFollowup? latest)
    {
        var baseDate = latest?.DueDate.Date
            ?? project.StartDate?.Date
            ?? project.CreatedAt.Date;
        return baseDate.AddDays(NormalizeInterval(project.FollowUpIntervalDays));
    }

    private static string FollowUpStatus(DateTime? dueDate)
    {
        if (!dueDate.HasValue) return "upcoming";
        var today = BusinessClock.Today;
        if (dueDate.Value.Date < today) return "overdue";
        if (dueDate.Value.Date == today) return "due";
        return "upcoming";
    }

    private static string? LabelFor(Dictionary<(string Kind, string Code), string> optionMap, string kind, string? code)
        => string.IsNullOrWhiteSpace(code) ? null : optionMap.GetValueOrDefault((kind, code.ToLowerInvariant()));

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
        EnsureMaxLength(request.Kind.Trim(), 50, "配置类型");
        EnsureMaxLength(request.Code.Trim(), 50, "配置编码");
        EnsureMaxLength(request.Label.Trim(), 100, "配置名称");
    }

    private static void ValidateProjectTextLengths(
        string name,
        string code,
        SaveTestProjectRequest request)
    {
        EnsureMaxLength(name, 100, "项目名称");
        EnsureMaxLength(code, 50, "项目编号");
        EnsureMaxLength(request.ProjectTypeCode, 50, "项目类型");
        EnsureMaxLength(request.ProgressCode, 50, "项目进度");
        EnsureMaxLength(request.TestStatus, 1000, "测试状态");
    }

    private static void EnsureMaxLength(string? value, int maxLength, string field)
    {
        if (value?.Trim().Length > maxLength)
            throw new BizException(4001, $"{field}不能超过 {maxLength} 个字符");
    }

    private async Task EnsureOptionCodeAvailable(string kind, string code, int? selfId = null)
    {
        if (await _db.TestProjectOptions.AnyAsync(x => x.Kind == kind && x.Code == code && x.Id != selfId))
        {
            throw new BizException(4094, "配置编码已存在");
        }
    }

    private async Task EnsureOptionCanChangeAsync(
        TestProjectOption option,
        string nextKind,
        string nextCode,
        bool nextIsActive)
    {
        if (option.Kind == OptionKindProgress
            && ReservedProgressCodes.Contains(option.Code)
            && (nextKind != option.Kind
                || !string.Equals(nextCode, option.Code, StringComparison.OrdinalIgnoreCase)
                || !nextIsActive))
        {
            throw new BizException(4094, "落地跟进和已结案是系统保留进度，不能改码、改类型或停用");
        }

        var isUsed = option.Kind switch
        {
            OptionKindProjectType => await _db.TestProjects.AnyAsync(x => x.ProjectTypeCode == option.Code),
            OptionKindProgress => await _db.TestProjects.AnyAsync(x => x.ProgressCode == option.Code),
            _ => false
        };
        if (!isUsed) return;
        if (option.Kind != nextKind || option.Code != nextCode)
            throw new BizException(4094, "配置项已被项目使用，不能修改类型或编码");
        if (!nextIsActive)
            throw new BizException(4094, "配置项已被项目使用，不能停用");
    }

    private async Task EnsureProjectUnique(string code, string name, int? selfId = null)
    {
        if (await _db.TestProjects.AnyAsync(x => x.Code == code && x.Id != selfId))
        {
            throw new BizException(4094, "项目编号已存在");
        }

        if (await _db.TestProjects.AnyAsync(x => x.Name == name && x.Id != selfId))
        {
            throw new BizException(4094, "项目名称已存在");
        }
    }

    private static string? NormalizeOptional(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static void NormalizeProjectCodes(SaveTestProjectRequest request)
    {
        request.ProjectTypeCode = NormalizeOptional(request.ProjectTypeCode)?.ToLowerInvariant();
        request.ProgressCode = NormalizeOptional(request.ProgressCode)?.ToLowerInvariant();
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is MySqlException { Number: 1062 };

    private static bool IsForeignKeyViolation(DbUpdateException ex)
        => ex.InnerException is MySqlException { Number: 1451 or 1452 };

    private static int NormalizeInterval(int days)
        => Math.Clamp(days <= 0 ? 14 : days, 1, 365);
}
