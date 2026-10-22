using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Globalization;

namespace AssetManagement.Infrastructure.TestMaterials;

public class TestProjectService : ITestProjectService
{
    public const string OptionKindProjectType = "project_type";
    public const string OptionKindProgress = "project_progress";
    public const string ProgressLanding = TestProjectLifecycleRules.Landing;
    public const string ProgressClosed = TestProjectLifecycleRules.Closed;
    private static readonly HashSet<string> ReservedProgressCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            TestProjectLifecycleRules.Planning,
            TestProjectLifecycleRules.Testing,
            ProgressLanding,
            ProgressClosed
        };

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly TestProjectAccessScope _accessScope;

    public TestProjectService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
        _accessScope = new TestProjectAccessScope(db, http);
    }

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
        var projectsQuery = ApplyProjectQuery(_db.TestProjects.AsNoTracking(), query);

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

    public async Task<byte[]> ExportAsync(TestProjectPageQuery query, int currentUserId)
    {
        var projectsQuery = ApplyProjectQuery(_db.TestProjects.AsNoTracking(), query);
        if (await projectsQuery.CountAsync() > AppConstants.MaxExportRows)
            throw new BizException(4130, $"导出项目不能超过 {AppConstants.MaxExportRows} 条，请缩小筛选范围");

        var projects = await projectsQuery.OrderBy(x => x.Code).ThenBy(x => x.Id).ToListAsync();
        var projectIds = projects.Select(x => x.Id).ToArray();
        var materials = await _db.TestMaterials.AsNoTracking()
            .Where(x => projectIds.Contains(x.ProjectId))
            .OrderBy(x => x.ProjectId)
            .ThenBy(x => x.MaterialNo)
            .ToListAsync();
        if (materials.Count > AppConstants.MaxExportRows)
            throw new BizException(4130, $"导出料件不能超过 {AppConstants.MaxExportRows} 条，请缩小筛选范围");

        var ownerIds = projects.Where(x => x.OwnerId.HasValue).Select(x => x.OwnerId!.Value)
            .Concat(materials.Where(x => x.CustodianId.HasValue).Select(x => x.CustodianId!.Value))
            .Distinct().ToArray();
        var userNames = await _db.Users.AsNoTracking().Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var departmentIds = materials.Where(x => x.DepartmentId.HasValue).Select(x => x.DepartmentId!.Value).Distinct().ToArray();
        var departmentNames = await _db.Departments.AsNoTracking().Where(x => departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var options = await _db.TestProjectOptions.AsNoTracking().ToListAsync();
        var optionLabels = options.ToDictionary(x => (x.Kind, x.Code), x => x.Label);
        var projectsById = projects.ToDictionary(x => x.Id);

        var projectRows = new List<string[]>
        {
            new[] { "项目编号", "项目名称", "项目类型", "项目进度", "负责人", "开始时间", "计划完成时间", "结案时间", "跟进间隔(天)", "项目摘要", "删除状态" }
        };
        projectRows.AddRange(projects.Select(x => new[]
        {
            x.Code ?? "",
            x.Name,
            OptionLabel(optionLabels, OptionKindProjectType, x.ProjectTypeCode),
            OptionLabel(optionLabels, OptionKindProgress, x.ProgressCode),
            x.OwnerId.HasValue ? userNames.GetValueOrDefault(x.OwnerId.Value, "") : "",
            FormatDate(x.StartDate),
            FormatDate(x.PlannedFinishDate),
            FormatDate(x.ClosedDate),
            NormalizeInterval(x.FollowUpIntervalDays).ToString(),
            x.TestStatus ?? "",
            x.IsDeleted ? "已删除" : "正常"
        }));

        var materialRows = new List<string[]>
        {
            new[] { "项目编号", "项目名称", "料件编号", "料件名称", "厂商", "型号", "品牌", "数量", "对接厂商", "部门", "位置", "保管人", "收件日期", "状态", "备注", "删除状态" }
        };
        materialRows.AddRange(materials.Select(x =>
        {
            var project = projectsById[x.ProjectId];
            return new[]
            {
                project.Code ?? "",
                project.Name,
                x.MaterialNo,
                x.Name,
                x.VendorName ?? "",
                x.Model ?? "",
                x.Brand ?? "",
                x.Quantity.ToString(),
                x.DockingVendorName ?? "",
                x.DepartmentId.HasValue ? departmentNames.GetValueOrDefault(x.DepartmentId.Value, "") : "",
                x.LocationName ?? "",
                x.CustodianId.HasValue ? userNames.GetValueOrDefault(x.CustodianId.Value, "") : "",
                FormatDate(x.ReceivedDate),
                x.Status == MaterialStatus.ReturnedToVendor ? "已退回厂商" : "在用",
                x.Remark ?? "",
                x.IsDeleted ? "已删除" : "正常"
            };
        }));

        return XlsxTable.Write(new[]
        {
            ("测试项目", (IEnumerable<string[]>)projectRows),
            ("测试料件", (IEnumerable<string[]>)materialRows)
        });
    }

    public async Task<byte[]> BuildImportTemplateAsync()
    {
        var options = await _db.TestProjectOptions.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToListAsync();
        var projectTypes = options.Where(x => x.Kind == OptionKindProjectType).ToList();
        var progressOptions = options.Where(x => x.Kind == OptionKindProgress).ToList();
        var importRows = new List<string[]>
        {
            new[]
            {
                "项目编号", "项目名称", "项目类型", "负责人工号", "开始时间",
                "计划完成时间", "项目进度", "结案时间", "跟进间隔(天)", "项目摘要"
            }
        };
        var instructions = new List<string[]>
        {
            new[] { "字段", "填写说明" },
            new[] { "项目编号", "必填，系统内唯一，最多 50 个字符" },
            new[] { "项目名称", "必填，系统内唯一，最多 100 个字符" },
            new[] { "项目类型", "必填，请填写下方列出的中文名称" },
            new[] { "负责人工号", "必填，必须是启用用户的工号" },
            new[] { "开始时间", "必填，格式 yyyy-MM-dd" },
            new[] { "计划完成时间", "必填，不能早于开始时间" },
            new[] { "项目进度", "必填，请填写下方列出的中文名称" },
            new[] { "结案时间", "仅已结案项目必填，格式 yyyy-MM-dd" },
            new[] { "跟进间隔(天)", "选填，留空默认 14，范围 1-365" },
            new[] { "项目摘要", "选填，最多 1000 个字符" },
            new[] { "", "" },
            new[] { "可用项目类型", "中文名称" }
        };
        instructions.AddRange(projectTypes.Select(x => new[] { "", x.Label }));
        instructions.Add(new[] { "", "" });
        instructions.Add(new[] { "可用项目进度", "中文名称" });
        instructions.AddRange(progressOptions.Select(x => new[] { "", x.Label }));

        return XlsxTable.Write(new[]
        {
            ("测试项目", (IEnumerable<string[]>)importRows),
            ("填写说明", (IEnumerable<string[]>)instructions)
        });
    }

    public async Task<TestProjectImportResultDto> ValidateImportAsync(Stream file)
    {
        var rawRows = XlsxTable.Read(file)
            .Skip(1)
            .Where(cells => cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();
        if (rawRows.Count > AppConstants.MaxImportRows)
            throw new BizException(4153, $"单次导入不能超过 {AppConstants.MaxImportRows} 行");

        var options = await _db.TestProjectOptions.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();
        var users = await _db.Users.AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        var existingCodes = (await _db.TestProjects.AsNoTracking()
                .Select(x => x.Code)
                .Where(x => x != null)
                .ToListAsync())
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNames = (await _db.TestProjects.AsNoTracking()
                .Select(x => x.Name)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = rawRows.Select((cells, index) => ValidateImportRow(
            index + 2,
            cells,
            options,
            users,
            existingCodes,
            existingNames,
            seenCodes,
            seenNames)).ToList();
        await ApplyImportOwnerPolicyAsync(rows);

        return BuildImportResult(rows);
    }

    public async Task<TestProjectImportResultDto> ConfirmImportAsync(Stream file)
    {
        var result = await ValidateImportAsync(file);
        var validRows = result.Rows.Where(x => x.IsValid).ToList();
        if (validRows.Count == 0) return result;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        foreach (var row in validRows)
        {
            _db.TestProjects.Add(new TestProject
            {
                Code = row.Code,
                Name = row.Name,
                ProjectTypeCode = row.ProjectTypeCode,
                OwnerId = row.OwnerId,
                StartDate = row.StartDate?.Date,
                PlannedFinishDate = row.PlannedFinishDate?.Date,
                ProgressCode = row.ProgressCode,
                ClosedDate = row.ClosedDate?.Date,
                FollowUpIntervalDays = row.FollowUpIntervalDays,
                TestStatus = row.TestStatus,
                CreatedAt = DateTime.UtcNow
            });
        }
        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            await transaction.RollbackAsync();
            throw new BizException(4094, "项目编号或项目名称已存在，请重新校验导入文件");
        }
        return result;
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
        ValidateInitialProjectProgress(request.ProgressCode);
        await EnsureProjectUnique(code, name);
        await ValidateProjectReferences(request);
        await _accessScope.EnsureOwnerAssignableAsync(request.OwnerId!.Value, creating: true);
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
        return (await ToDtos(new[] { project }, new Dictionary<int, int>(), _accessScope.CurrentUserId)).Single();
    }

    public async Task<TestProjectDto> UpdateAsync(int id, SaveTestProjectRequest request)
    {
        NormalizeProjectCodes(request);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        if (_accessScope.CurrentUserId is int currentUserId)
            await _accessScope.EnsureScopeAsync(project, currentUserId);
        EnsureProjectCanChange(project);
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new BizException(4001, "项目名称不能为空");
        var code = (request.Code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code)) throw new BizException(4001, "项目编号不能为空");
        ValidateProjectTextLengths(name, code, request);
        ValidateProjectRequiredFields(request);
        ValidateProjectTimeline(request);
        EnsureProjectVersion(project, request.RowVersion);
        ValidateSupportedProgress(request.ProgressCode);
        await EnsureProjectUnique(code, name, id);
        await ValidateProjectReferences(request);
        await _accessScope.EnsureOwnerAssignableAsync(request.OwnerId!.Value, creating: false);
        await EnsureProjectStagesFitTimelineAsync(id, request.StartDate, request.ClosedDate);
        ValidateProgressTransition(project.ProgressCode, request.ProgressCode);
        if (ShouldRequireTestingCompletion(project.ProgressCode, request.ProgressCode))
            await EnsureTestingCompletionAsync(id, request.ProgressCode, request.ClosedDate);
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
        await transaction.CommitAsync();
        var count = await _db.TestMaterials.CountAsync(x => !x.IsDeleted && x.ProjectId == id);
        return (await ToDtos(new[] { project }, new Dictionary<int, int> { [id] = count }, _accessScope.CurrentUserId)).Single();
    }

    public async Task<TestProjectDto> ReopenAsync(int id, uint? rowVersion)
    {
        if (!_accessScope.IsAdmin)
            throw new BizException(4031, "只有系统管理员可以重新打开已结案项目");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        if (!project.ClosedDate.HasValue &&
            !string.Equals(project.ProgressCode, ProgressClosed, StringComparison.OrdinalIgnoreCase))
            throw new BizException(4099, "项目未结案，无需重新打开");

        EnsureProjectVersion(project, rowVersion);
        project.ClosedDate = null;
        project.ProgressCode = TestProjectLifecycleRules.Testing;
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
        var count = await _db.TestMaterials.CountAsync(x => !x.IsDeleted && x.ProjectId == id);
        return (await ToDtos(
            new[] { project },
            new Dictionary<int, int> { [id] = count },
            _accessScope.CurrentUserId)).Single();
    }

    public async Task<TestProjectDto> UpdateProgressAsync(
        int id,
        UpdateTestProjectProgressRequest request,
        int currentUserId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4046, "测试项目不存在");
        if (project.IsDeleted) throw new BizException(4046, "测试项目不存在");
        if (!await _accessScope.CanExecuteAsync(project, currentUserId, "project:manage", true))
            throw new BizException(4031, "只有项目负责人、项目管理人员或管理员可以更新项目进展");
        EnsureProjectCanChange(project);
        EnsureProjectVersion(project, request.RowVersion);

        var progressCode = request.ProgressCode?.Trim();
        if (string.IsNullOrWhiteSpace(progressCode))
            throw new BizException(4001, "项目进度不能为空");
        ValidateSupportedProgress(progressCode);
        if (!await _db.TestProjectOptions.AnyAsync(x =>
                x.Kind == OptionKindProgress && x.Code == progressCode && x.IsActive))
            throw new BizException(4002, "项目进度不存在或已停用");

        var closedDate = request.ClosedDate?.Date;
        ValidateProgressTransition(project.ProgressCode, progressCode);
        if (ShouldRequireTestingCompletion(project.ProgressCode, progressCode))
            await EnsureTestingCompletionAsync(id, progressCode, closedDate);
        ValidateProjectProgressTimeline(project.StartDate, progressCode, closedDate);
        var testStatus = request.TestStatus?.Trim();
        if (testStatus?.Length > 1000)
            throw new BizException(4001, "项目摘要不能超过1000个字符");

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

        await transaction.CommitAsync();

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
        if (_accessScope.CurrentUserId is int currentUserId)
            await _accessScope.EnsureScopeAsync(project, currentUserId);
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
        if (_accessScope.CurrentUserId is int currentUserId)
            await _accessScope.EnsureScopeAsync(project, currentUserId);
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
        if (_accessScope.CurrentUserId is int currentUserId)
            await _accessScope.EnsureScopeAsync(project, currentUserId);
        if (!project.IsDeleted) throw new BizException(4097, "请先删除项目后再彻底删除");
        if (await _db.TestMaterials.AnyAsync(x => x.ProjectId == id))
            throw new BizException(4092, "该项目下仍有测试料件(含已删除),不能彻底删除");
        if (await _db.TestProjectFollowups.AnyAsync(x => x.ProjectId == id))
            throw new BizException(4092, "该项目仍有跟进历史,不能彻底删除");
        if (await _db.ProjectTestStages.AnyAsync(x => x.ProjectId == id) ||
            await _db.ProjectReportDocuments.AnyAsync(x => x.ProjectId == id))
            throw new BizException(4092, "该项目仍有测试阶段或报告归档，不能彻底删除");
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
        EnsureSupportedProgressOption(kind, code);
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
        if (kind == OptionKindProgress && !ReservedProgressCodes.Contains(code) &&
            (option.Kind != OptionKindProgress || option.Code != code || request.IsActive))
            throw new BizException(4094, "历史自定义项目进度已废弃，请停用或删除后使用系统生命周期状态");
        EnsureSupportedProgressOption(kind, code, allowExisting: true);
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
            throw new BizException(4094, "系统生命周期进度不能删除");
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
        var dueDate = BusinessClock.Today;
        if (request.DueDate.HasValue)
        {
            EnsureFollowupDate(request.DueDate.Value.Date);
            if (request.DueDate.Value.Date != dueDate)
                throw new BizException(4001, "跟进日期由系统自动记录，不能手动指定");
        }
        var latestDueDate = await _db.TestProjectFollowups
            .Where(x => x.ProjectId == projectId)
            .Select(x => (DateTime?)x.DueDate)
            .MaxAsync();
        if (latestDueDate.HasValue && dueDate < latestDueDate.Value.Date)
        {
            throw new BizException(
                4001,
                $"跟进日期不能早于上一条跟进记录（{latestDueDate.Value:yyyy-MM-dd}），同一天可以新增");
        }

        var followup = new TestProjectFollowup
        {
            ProjectId = projectId,
            DueDate = dueDate,
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
        await EnsureLatestFollowupAsync(projectId, followup.Id);
        var content = (request.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content)) throw new BizException(4001, "请填写落地情况");
        EnsureMaxLength(content, 2000, "落地情况");
        if (request.DueDate.HasValue && request.DueDate.Value.Date != followup.DueDate.Date)
        {
            throw new BizException(4094, "历史跟进日期不可修改，请只修改跟进内容");
        }

        // 跟进日期是历史记录的业务归属，只能在新增时确定，编辑时保持不变。
        EnsureFollowupDate(followup.DueDate);
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
        await EnsureLatestFollowupAsync(projectId, followup.Id);
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
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
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
            Year = year,
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
        var userId = currentUserId.GetValueOrDefault();
        if (userId > 0)
            await _accessScope.PreloadAsync(list, userId);

        return list.Select(x =>
        {
            latestByProject.TryGetValue(x.Id, out var latest);
            var isLanding = IsLandingProgress(x);
            var isClosed = x.ClosedDate.HasValue ||
                string.Equals(x.ProgressCode, ProgressClosed, StringComparison.OrdinalIgnoreCase);
            var inScope = userId > 0 && _accessScope.IsInDepartmentScope(x.OwnerId, userId);
            var canChange = !x.IsDeleted && !x.ClosedDate.HasValue;
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
                CanWriteFollowUp = !x.IsDeleted && isLanding &&
                    CanExecuteLoaded(x, userId, "project:followup", ownerMayExecute: true),
                CanEdit = canChange && CanExecuteLoaded(x, userId, "project:edit", ownerMayExecute: false),
                CanDelete = !x.IsDeleted && inScope && _accessScope.HasPermission("project:delete"),
                CanRestore = x.IsDeleted && inScope && _accessScope.HasPermission("project:restore"),
                CanPurge = x.IsDeleted && inScope && _accessScope.HasPermission("project:purge"),
                CanUpdateProgress = canChange &&
                    CanExecuteLoaded(x, userId, "project:manage", ownerMayExecute: true),
                CanReopen = _accessScope.IsAdmin && !x.IsDeleted && isClosed,
                CreatedAt = x.CreatedAt,
                IsDeleted = x.IsDeleted,
                DeletedAt = x.DeletedAt,
                MaterialCount = counts.GetValueOrDefault(x.Id),
                RowVersion = x.RowVersion
            };
        }).ToList();
    }

    private bool CanExecuteLoaded(TestProject project, int userId, string permission, bool ownerMayExecute)
    {
        if (userId <= 0) return false;
        if (_accessScope.IsAdmin) return true;
        if (ownerMayExecute && project.OwnerId == userId) return true;
        return _accessScope.HasPermission(permission) &&
               _accessScope.IsInDepartmentScope(project.OwnerId, userId);
    }

    private async Task<List<TestProjectFollowupDto>> ToFollowupDtos(IEnumerable<TestProjectFollowup> followups)
    {
        var list = followups.ToList();
        var userIds = list.Select(x => x.FilledById).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var projectIds = list.Select(x => x.ProjectId).Distinct().ToArray();
        var latestIds = (await _db.TestProjectFollowups.AsNoTracking()
                .Where(x => projectIds.Contains(x.ProjectId))
                .Select(x => new { x.Id, x.ProjectId, x.DueDate })
                .ToListAsync())
            .GroupBy(x => x.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(x => x.DueDate).ThenByDescending(x => x.Id).First().Id);
        return list.Select(x => new TestProjectFollowupDto
        {
            Id = x.Id,
            ProjectId = x.ProjectId,
            DueDate = x.DueDate,
            Content = x.Content,
            FilledById = x.FilledById,
            FilledByName = users.GetValueOrDefault(x.FilledById),
            FilledAt = x.FilledAt,
            IsLatest = latestIds.TryGetValue(x.ProjectId, out var latestId) && latestId == x.Id
        }).ToList();
    }

    private async Task ApplyImportOwnerPolicyAsync(List<TestProjectImportRowDto> rows)
    {
        if (_accessScope.IsAdmin) return;
        var ownerIds = rows.Where(x => x.OwnerId.HasValue).Select(x => x.OwnerId!.Value).Distinct().ToArray();
        if (_accessScope.CurrentUserId is int userId && userId > 0)
            await _accessScope.PreloadAsync(ownerIds.Select(id => new TestProject { OwnerId = id }), userId);

        foreach (var row in rows)
        {
            var error = await _accessScope.OwnerAssignmentErrorAsync(row.OwnerId, creating: true);
            if (error is null) continue;
            row.IsValid = false;
            row.Error = string.IsNullOrEmpty(row.Error) ? error : $"{row.Error}；{error}";
        }
    }

    private async Task EnsureLatestFollowupAsync(int projectId, int followupId)
    {
        var latestId = await _db.TestProjectFollowups.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
        if (latestId != followupId)
            throw new BizException(4094, "只能修改或删除最近一条跟进记录");
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
        if (closedDate.HasValue && closedDate.Value.Date > BusinessClock.Today)
            throw new BizException(4001, "结案时间不能晚于今天");
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
        if (closedDate.HasValue && closedDate.Value.Date > BusinessClock.Today)
            throw new BizException(4001, "结案时间不能晚于今天");
        if (isClosed && !closedDate.HasValue)
            throw new BizException(4001, "已结案项目必须填写结案时间");
        if (!isClosed && closedDate.HasValue)
            throw new BizException(4001, "只有已结案项目才能填写结案时间");
    }

    private static void ValidateProgressTransition(string? currentProgressCode, string? nextProgressCode)
    {
        if (TestProjectLifecycleRules.CanTransition(currentProgressCode, nextProgressCode)) return;
        if (string.Equals(currentProgressCode, TestProjectLifecycleRules.Planning, StringComparison.OrdinalIgnoreCase))
            throw new BizException(4094, "计划中的项目只能保持计划中或进入测试中");
        throw new BizException(4094, "项目进度不能回退");
    }

    private static bool ShouldRequireTestingCompletion(
        string? currentProgressCode,
        string? nextProgressCode) =>
        IsTerminalTestingProgress(nextProgressCode) &&
        (!IsTerminalTestingProgress(currentProgressCode) ||
         string.Equals(nextProgressCode, ProgressClosed, StringComparison.OrdinalIgnoreCase));

    private async Task EnsureTestingCompletionAsync(
        int projectId,
        string? progressCode,
        DateTime? requestedClosedDate)
    {
        if (!IsTerminalTestingProgress(progressCode))
            return;

        if (await _db.MaterialFlows.AnyAsync(flow =>
                flow.Status == "pending" &&
                _db.TestMaterials.Any(material =>
                    material.Id == flow.MaterialId && material.ProjectId == projectId)))
            throw new BizException(4092, "项目存在进行中的料件流转，请先处理完再进入落地跟进或结案");

        var stages = await _db.ProjectTestStages.AsNoTracking()
            .Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .OrderByDescending(x => x.Sequence)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var finalStage = stages.FirstOrDefault();
        if (finalStage is null ||
            stages.Any(x => !string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase)) ||
            !string.Equals(finalStage.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(finalStage.TestContent) ||
            string.IsNullOrWhiteSpace(finalStage.TestResult))
            throw new BizException(4094, "进入落地跟进或结案前，必须完成所有测试阶段并填写测试结果");

        if (requestedClosedDate.HasValue)
        {
            if (requestedClosedDate.Value.Date > BusinessClock.Today)
                throw new BizException(4001, "结案时间不能晚于今天");
            var latestStageEndDate = stages.Where(x => x.EndDate.HasValue)
                .Select(x => x.EndDate!.Value.Date)
                .DefaultIfEmpty()
                .Max();
            if (latestStageEndDate != default && requestedClosedDate.Value.Date < latestStageEndDate)
                throw new BizException(4001, "结案时间不能早于所有测试阶段的结束日期");
        }

        if (!await _db.ProjectReportDocuments.AnyAsync(x =>
                x.ProjectId == projectId &&
                x.StageId == finalStage.Id &&
                !x.IsDeleted))
            throw new BizException(4094, "进入落地跟进或结案前，必须上传最后测试阶段报告");
    }

    private static bool IsTerminalTestingProgress(string? progressCode) =>
        TestProjectLifecycleRules.IsTerminal(progressCode);

    private static void ValidateInitialProjectProgress(string? progressCode)
    {
        if (!TestProjectLifecycleRules.IsInitialProjectProgressAllowed(progressCode))
            throw new BizException(4094, "新建或导入项目只能处于计划中或测试中，请按流程推进项目");
    }

    private static void ValidateSupportedProgress(string? progressCode)
    {
        if (!TestProjectLifecycleRules.IsSupportedProgress(progressCode))
            throw new BizException(4094, "项目进度只能使用计划中、测试中、落地跟进或已结案");
    }

    private static void EnsureSupportedProgressOption(
        string kind,
        string code,
        bool allowExisting = false)
    {
        if (kind == OptionKindProgress && !ReservedProgressCodes.Contains(code) && !allowExisting)
            throw new BizException(4094, "项目进度由系统生命周期控制，不能新增自定义进度");
    }

    private async Task EnsureProjectStagesFitTimelineAsync(
        int projectId,
        DateTime? projectStartDate,
        DateTime? requestedClosedDate)
    {
        var stages = await _db.ProjectTestStages.AsNoTracking()
            .Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .ToListAsync();
        if (projectStartDate.HasValue && stages.Any(x =>
                (x.StartDate.HasValue && x.StartDate.Value.Date < projectStartDate.Value.Date) ||
                (x.EndDate.HasValue && x.EndDate.Value.Date < projectStartDate.Value.Date)))
            throw new BizException(4001, "项目开始时间不能晚于已有测试阶段日期");
        if (requestedClosedDate.HasValue && stages.Any(x =>
                x.EndDate.HasValue && x.EndDate.Value.Date > requestedClosedDate.Value.Date))
            throw new BizException(4001, "结案时间不能早于所有测试阶段的结束日期");
    }

    private static IQueryable<TestProject> ApplyProjectQuery(
        IQueryable<TestProject> projectsQuery,
        TestProjectPageQuery query)
    {
        var status = query.DeleteStatus?.Trim().ToLowerInvariant();
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
        var ownerIds = query.OwnerIds is { Length: > 0 }
            ? query.OwnerIds
            : query.OwnerId is int ownerId ? [ownerId] : [];
        if (ownerIds.Length > 0)
            projectsQuery = projectsQuery.Where(x => x.OwnerId.HasValue && ownerIds.Contains(x.OwnerId.Value));
        var progressCodes = (query.ProgressCodes is { Length: > 0 }
                ? query.ProgressCodes
                : string.IsNullOrWhiteSpace(query.ProgressCode) ? [] : new[] { query.ProgressCode })
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToArray();
        if (progressCodes.Length > 0)
            projectsQuery = projectsQuery.Where(x => x.ProgressCode != null && progressCodes.Contains(x.ProgressCode));
        var projectTypeCodes = (query.ProjectTypeCodes is { Length: > 0 }
                ? query.ProjectTypeCodes
                : string.IsNullOrWhiteSpace(query.ProjectTypeCode) ? [] : new[] { query.ProjectTypeCode })
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToArray();
        if (projectTypeCodes.Length > 0)
            projectsQuery = projectsQuery.Where(x => x.ProjectTypeCode != null && projectTypeCodes.Contains(x.ProjectTypeCode));
        return projectsQuery;
    }

    private static string OptionLabel(
        IReadOnlyDictionary<(string Kind, string Code), string> optionLabels,
        string kind,
        string? code)
        => string.IsNullOrWhiteSpace(code) ? "" : optionLabels.GetValueOrDefault((kind, code), code);

    private static string FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd") ?? "";

    private static void EnsureProjectCanChange(TestProject project)
    {
        if (project.ClosedDate.HasValue)
            throw new BizException(4094, "项目已结案，不能再修改");
    }

    private static void EnsureProjectVersion(TestProject project, uint? requestedVersion)
    {
        if (!requestedVersion.HasValue || requestedVersion.Value != project.RowVersion)
            throw new BizException(4090, "该项目已被其他人更新，请刷新后重试");
    }

    private static void EnsureFollowupDate(DateTime dueDate)
    {
        if (dueDate.Date > BusinessClock.Today)
            throw new BizException(4001, "跟进日期不能晚于今天");
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
        // 落地跟进不是普通项目编辑权限：只有进入落地阶段后，负责人、跟进管理人员或管理员才能填写。
        if (!IsLandingProgress(project))
            throw new BizException(4031, "项目进入落地跟进后才能填写落地跟进");
        if (await _accessScope.CanExecuteAsync(project, currentUserId, "project:followup", true)) return;
        throw new BizException(4031, "只有项目负责人、跟进管理人员或管理员可以填写落地跟进");
    }

    private static bool IsLandingProgress(TestProject project)
        => string.Equals(project.ProgressCode, ProgressLanding, StringComparison.OrdinalIgnoreCase);

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
            throw new BizException(4094, "系统生命周期进度不能改码、改类型或停用");
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

    private static TestProjectImportResultDto BuildImportResult(List<TestProjectImportRowDto> rows)
        => new()
        {
            SuccessCount = rows.Count(x => x.IsValid),
            FailedCount = rows.Count(x => !x.IsValid),
            Rows = rows
        };

    private static TestProjectImportRowDto ValidateImportRow(
        int rowNumber,
        IReadOnlyList<string> cells,
        IReadOnlyList<TestProjectOption> options,
        IReadOnlyDictionary<string, User> users,
        IReadOnlySet<string> existingCodes,
        IReadOnlySet<string> existingNames,
        ISet<string> seenCodes,
        ISet<string> seenNames)
    {
        var code = ImportCell(cells, 0);
        var name = ImportCell(cells, 1);
        var projectTypeText = ImportCell(cells, 2);
        var ownerEmployeeNo = ImportCell(cells, 3);
        var startDateText = ImportCell(cells, 4);
        var plannedFinishDateText = ImportCell(cells, 5);
        var progressText = ImportCell(cells, 6);
        var closedDateText = ImportCell(cells, 7);
        var intervalText = ImportCell(cells, 8);
        var testStatus = NormalizeOptional(ImportCell(cells, 9));
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(code)) errors.Add("项目编号必填");
        else
        {
            if (code.Length > 50) errors.Add("项目编号不能超过 50 个字符");
            if (existingCodes.Contains(code)) errors.Add("项目编号已存在");
            if (!seenCodes.Add(code)) errors.Add("项目编号在文件中重复");
        }
        if (string.IsNullOrWhiteSpace(name)) errors.Add("项目名称必填");
        else
        {
            if (name.Length > 100) errors.Add("项目名称不能超过 100 个字符");
            if (existingNames.Contains(name)) errors.Add("项目名称已存在");
            if (!seenNames.Add(name)) errors.Add("项目名称在文件中重复");
        }

        var projectType = ResolveImportOption(options, OptionKindProjectType, projectTypeText);
        if (projectType is null) errors.Add("项目类型不存在或已停用");
        var progress = ResolveImportOption(options, OptionKindProgress, progressText);
        if (progress is null) errors.Add("项目进度不存在或已停用");
        else if (!TestProjectLifecycleRules.IsSupportedProgress(progress.Code))
            errors.Add("项目进度只能使用计划中、测试中、落地跟进或已结案");

        users.TryGetValue(ownerEmployeeNo, out var owner);
        if (string.IsNullOrWhiteSpace(ownerEmployeeNo)) errors.Add("负责人工号必填");
        else if (owner is null) errors.Add("负责人不存在或已停用");

        var startDate = ParseImportDate(startDateText, "开始时间", required: true, errors);
        var plannedFinishDate = ParseImportDate(plannedFinishDateText, "计划完成时间", required: true, errors);
        var closedDate = ParseImportDate(closedDateText, "结案时间", required: false, errors);
        var interval = 14;
        if (!string.IsNullOrWhiteSpace(intervalText)
            && (!int.TryParse(intervalText, out interval) || interval is < 1 or > 365))
        {
            errors.Add("跟进间隔必须是 1-365 的整数");
            interval = 14;
        }
        if (testStatus?.Length > 1000) errors.Add("项目摘要不能超过 1000 个字符");
        if (startDate.HasValue && plannedFinishDate.HasValue && plannedFinishDate.Value < startDate.Value)
            errors.Add("计划完成时间不能早于开始时间");
        if (startDate.HasValue && closedDate.HasValue && closedDate.Value < startDate.Value)
            errors.Add("结案时间不能早于开始时间");
        if (progress is not null)
        {
            var isClosed = string.Equals(progress.Code, ProgressClosed, StringComparison.OrdinalIgnoreCase);
            if (TestProjectLifecycleRules.IsTerminal(progress.Code))
                errors.Add("新建或导入项目只能处于计划中或测试中，请按流程推进项目");
            if (isClosed && !closedDate.HasValue) errors.Add("已结案项目必须填写结案时间");
            if (!isClosed && closedDate.HasValue) errors.Add("只有已结案项目才能填写结案时间");
        }

        return new TestProjectImportRowDto
        {
            Row = rowNumber,
            Code = code,
            Name = name,
            ProjectTypeCode = projectType?.Code ?? projectTypeText,
            ProjectTypeLabel = projectType?.Label ?? "",
            OwnerEmployeeNo = ownerEmployeeNo,
            OwnerId = owner?.Id,
            OwnerName = owner?.Name ?? "",
            StartDate = startDate,
            PlannedFinishDate = plannedFinishDate,
            ProgressCode = progress?.Code ?? progressText,
            ProgressLabel = progress?.Label ?? "",
            ClosedDate = closedDate,
            FollowUpIntervalDays = interval,
            TestStatus = testStatus,
            IsValid = errors.Count == 0,
            Error = string.Join("；", errors)
        };
    }

    private static TestProjectOption? ResolveImportOption(
        IEnumerable<TestProjectOption> options,
        string kind,
        string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var matching = options.Where(x => x.Kind == kind).ToList();
        var byCode = matching.FirstOrDefault(x =>
            x.Code.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (byCode is not null) return byCode;
        var byLabel = matching.Where(x =>
            x.Label.Equals(text, StringComparison.OrdinalIgnoreCase)).ToList();
        return byLabel.Count == 1 ? byLabel[0] : null;
    }

    private static DateTime? ParseImportDate(
        string text,
        string field,
        bool required,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required) errors.Add($"{field}必填");
            return null;
        }
        if (DateTime.TryParseExact(
                text,
                new[] { "yyyy-MM-dd", "yyyy/M/d", "yyyy/M/dd", "yyyy/MM/d", "yyyy/MM/dd" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exact))
        {
            return exact.Date;
        }
        if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial)
            && serial is >= 1 and <= 2958465)
        {
            try
            {
                return DateTime.FromOADate(serial).Date;
            }
            catch (ArgumentException)
            {
                // 继续返回统一的格式错误。
            }
        }
        errors.Add($"{field}格式不正确");
        return null;
    }

    private static string ImportCell(IReadOnlyList<string> cells, int index)
        => index < cells.Count ? cells[index].Trim() : "";

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
