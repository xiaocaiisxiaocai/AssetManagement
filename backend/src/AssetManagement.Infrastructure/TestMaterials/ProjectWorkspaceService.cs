using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AssetManagement.Infrastructure.TestMaterials;

public class ProjectWorkspaceService : IProjectWorkspaceService
{
    private readonly AppDbContext _db;
    private readonly ProjectReportStorage _storage;
    private readonly IHttpContextAccessor _http;
    private readonly TestProjectAccessScope _accessScope;

    public ProjectWorkspaceService(AppDbContext db, ProjectReportStorage storage, IHttpContextAccessor http)
    {
        _db = db;
        _storage = storage;
        _http = http;
        _accessScope = new TestProjectAccessScope(db, http);
    }

    public async Task<ProjectWorkspaceDto> GetAsync(int projectId, int userId)
    {
        var project = await LoadProjectAsync(projectId);
        await _accessScope.EnsureCanViewAsync(project, userId);
        return new ProjectWorkspaceDto
        {
            ProjectId = projectId,
            CanManage = await CanMaintainStagesAsync(project) &&
                await CanManageAsync(project, userId),
            CanExport = !project.IsDeleted &&
                await _accessScope.CanExecuteAsync(project, userId, "project:export", true),
            Stages = (await _db.ProjectTestStages.AsNoTracking()
                .Where(x => x.ProjectId == projectId && !x.IsDeleted)
                .OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToListAsync()).Select(StageDto).ToList(),
            Documents = (await _db.ProjectReportDocuments.AsNoTracking()
                .Where(x => x.ProjectId == projectId && !x.IsDeleted)
                .OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.Id).ToListAsync()).Select(DocumentDto).ToList()
        };
    }

    public async Task<ProjectTestStageDto> SaveStageAsync(int projectId, int? stageId, SaveProjectTestStageRequest request, int userId)
    {
        ValidateStage(request);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var project = await LockWritableProjectAsync(projectId, userId);
        ValidateStage(request, project.StartDate);
        if (!stageId.HasValue &&
            !string.Equals(project.ProgressCode, "testing", StringComparison.OrdinalIgnoreCase))
            throw new BizException(4092, "项目进入测试中后才能新增测试阶段");
        ProjectTestStage stage;
        if (stageId.HasValue)
        {
            stage = await _db.ProjectTestStages.AsTracking()
                .SingleOrDefaultAsync(x => x.Id == stageId && x.ProjectId == projectId && !x.IsDeleted)
                ?? throw new BizException(4046, "测试阶段不存在");
            if (stage.RowVersion != request.RowVersion)
                throw new BizException(4090, "该阶段已被更新，请刷新后重试；当前输入可保留后再编辑");
        }
        else
        {
            stage = new ProjectTestStage { ProjectId = projectId, CreatedAt = DateTime.UtcNow };
            _db.ProjectTestStages.Add(stage);
        }
        if (await _db.ProjectTestStages.AnyAsync(x => x.ProjectId == projectId && !x.IsDeleted &&
                x.Id != (stageId ?? 0) && x.Sequence == request.Sequence))
            throw new BizException(4094, "该阶段序号已存在，请使用其他序号");
        if (request.StartDate.HasValue)
        {
            var previous = await _db.ProjectTestStages
                .Where(x => x.ProjectId == projectId && !x.IsDeleted &&
                    x.Id != (stageId ?? 0) &&
                    x.Sequence < request.Sequence &&
                    x.StartDate != null && x.EndDate == null)
                .OrderByDescending(x => x.Sequence)
                .Select(x => new { x.Sequence, x.Name })
                .FirstOrDefaultAsync();
            if (previous != null)
                throw new BizException(4092, $"请先结束第{previous.Sequence}阶段「{previous.Name}」");
        }
        if (IsCurrentStage(request.StartDate, request.EndDate) && await _db.ProjectTestStages.AnyAsync(x =>
                x.ProjectId == projectId && !x.IsDeleted && x.Id != (stageId ?? 0) &&
                x.StartDate != null && x.EndDate == null))
            throw new BizException(4092, "请先填写当前阶段的结束日期");

        stage.Sequence = request.Sequence;
        stage.Name = request.Name.Trim();
        stage.Objective = Optional(request.Objective);
        stage.TestContent = Optional(request.TestContent);
        stage.TestResult = Optional(request.TestResult);
        stage.StartDate = request.StartDate?.Date;
        stage.EndDate = request.EndDate?.Date;
        stage.Status = DeriveStageStatus(stage.StartDate, stage.EndDate);
        stage.RowVersion++;
        stage.UpdatedAt = DateTime.UtcNow;
        await SaveChangesAsync();
        await transaction.CommitAsync();
        return StageDto(stage);
    }

    public async Task DeleteStageAsync(int projectId, int stageId, uint rowVersion, int userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockWritableProjectAsync(projectId, userId);
        var stage = await _db.ProjectTestStages.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == stageId && x.ProjectId == projectId && !x.IsDeleted)
            ?? throw new BizException(4046, "测试阶段不存在");
        if (stage.RowVersion != rowVersion) throw new BizException(4090, "该阶段已被更新，请刷新后重试");
        if (await _db.ProjectReportDocuments.AnyAsync(x => x.StageId == stageId && !x.IsDeleted))
            throw new BizException(4092, "该阶段已有关联报告，请先处理关联报告后再删除");
        stage.IsDeleted = true;
        stage.RowVersion++;
        stage.UpdatedAt = DateTime.UtcNow;
        await SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<ProjectReportDocumentDto> UploadAsync(int projectId, int? stageId, string? description,
        string fileName, long length, Stream content, int userId)
    {
        var project = await LoadProjectAsync(projectId);
        await EnsureWritableAsync(project, userId);
        if (!stageId.HasValue)
            throw new BizException(4001, "报告必须关联测试阶段，结案阶段报告作为项目总报告");
        if (description?.Length > 500) throw new BizException(4001, "报告说明不能超过 500 个字符");
        await CheckStageAsync(projectId, stageId);
        var file = await _storage.SaveAsync(fileName, length, content);
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await LockWritableProjectAsync(projectId, userId);
            await CheckStageAsync(projectId, stageId);
            if (await _db.ProjectReportDocuments.CountAsync(x => x.ProjectId == projectId && !x.IsDeleted) >= 1000)
                throw new BizException(4001, "单个项目最多归档 1000 份报告");
            var name = await _db.Users.Where(x => x.Id == userId).Select(x => x.Name).SingleAsync();
            var document = new ProjectReportDocument
            {
                ProjectId = projectId, StageId = stageId, FileName = file.FileName,
                StoredName = file.StoredName, Size = file.Size, ContentType = file.ContentType,
                Description = Optional(description), UploadedById = userId,
                UploadedBy = name, UploadedAt = DateTime.UtcNow
            };
            _db.ProjectReportDocuments.Add(document);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return DocumentDto(document);
        }
        catch
        {
            _storage.RemoveUncommitted(file.StoredName);
            throw;
        }
    }

    public async Task<ProjectWorkspaceFile> DownloadAsync(int projectId, int documentId, int userId)
    {
        var project = await LoadProjectAsync(projectId);
        await _accessScope.EnsureCanViewAsync(project, userId);
        var document = await _db.ProjectReportDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && !x.IsDeleted)
            ?? throw new BizException(4046, "报告不存在或已删除");
        return new ProjectWorkspaceFile(_storage.Open(document.StoredName), document.ContentType, document.FileName);
    }

    public async Task DeleteDocumentAsync(int projectId, int documentId, int userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockWritableProjectAsync(projectId, userId);
        var document = await _db.ProjectReportDocuments.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && !x.IsDeleted)
            ?? throw new BizException(4046, "报告不存在或已删除");
        // 保留原文件以便审计和归档；业务下载与导出仅查询未删除记录。
        document.IsDeleted = true;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<ProjectWorkspaceFile> ExportAsync(int projectId, int? stageId, bool package, int userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var project = await LoadProjectAsync(projectId);
        await _accessScope.EnsureCanViewAsync(project, userId);
        if (!await _accessScope.CanExecuteAsync(project, userId, "project:export", true))
            throw new BizException(4031, "只有项目负责人或拥有项目导出权限的用户可以导出报告");
        await CheckStageAsync(projectId, stageId);
        var data = await ReportDataAsync(project, stageId);
        if (data.Flows.Count > 10000) throw new BizException(4130, "料件流转历史超过导出上限，请按阶段导出");
        var docx = ProjectReportBuilder.BuildDocx(data);
        var baseName = $"项目-{projectId}" + (stageId.HasValue ? $"-阶段-{data.Stages.Single().Sequence}" : "-完整测试");
        if (!package)
        {
            await transaction.CommitAsync();
            return new ProjectWorkspaceFile(new MemoryStream(docx),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{baseName}-报告.docx");
        }
        if (data.Documents.Sum(x => x.Size) > 200 * 1024L * 1024L)
            throw new BizException(4130, "资料包附件合计超过 200MB，请按阶段分别导出");
        var documents = await _db.ProjectReportDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && !x.IsDeleted && (!stageId.HasValue || x.StageId == stageId))
            .OrderBy(x => x.Id).ToListAsync();
        var output = new MemoryStream();
        try
        {
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                await AddEntryAsync(zip, "测试报告.docx", new MemoryStream(docx));
                await AddEntryAsync(zip, "测试报告.html", new MemoryStream(Encoding.UTF8.GetBytes(ProjectReportBuilder.BuildHtml(data))));
                foreach (var document in documents)
                    await AddEntryAsync(zip, $"documents/{document.Id}/{document.FileName}", _storage.Open(document.StoredName));
            }
            await transaction.CommitAsync();
            output.Position = 0;
            return new ProjectWorkspaceFile(output, "application/zip", $"{baseName}-资料包.zip");
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    private async Task<ProjectReportData> ReportDataAsync(TestProject project, int? stageId)
    {
        var options = await _db.TestProjectOptions.AsNoTracking().ToListAsync();
        var materials = await _db.TestMaterials.AsNoTracking().Where(x => x.ProjectId == project.Id)
            .OrderBy(x => x.MaterialNo).Take(10001).ToListAsync();
        if (materials.Count > 10000) throw new BizException(4130, "项目料件超过导出上限");
        var custodianIds = materials.Select(x => x.CustodianId).ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => x.Id == project.OwnerId || custodianIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var deptIds = materials.Select(x => x.DepartmentId).ToArray();
        var depts = await _db.Departments.Where(x => deptIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var materialIds = materials.Select(x => x.Id).ToArray();
        var lastFollowupDate = await _db.TestProjectFollowups.Where(x => x.ProjectId == project.Id)
            .MaxAsync(x => (DateTime?)x.DueDate);
        var nextFollowup = (lastFollowupDate ?? project.StartDate ?? project.CreatedAt).Date
            .AddDays(project.FollowUpIntervalDays);
        return new ProjectReportData
        {
            StageId = stageId,
            Project = new TestProjectDto
            {
                Id = project.Id, Name = project.Name, Code = project.Code,
                ProjectTypeCode = project.ProjectTypeCode,
                ProjectTypeLabel = options.FirstOrDefault(x => x.Kind == "project_type" && x.Code == project.ProjectTypeCode)?.Label,
                ProgressCode = project.ProgressCode,
                ProgressLabel = options.FirstOrDefault(x => x.Kind == "project_progress" && x.Code == project.ProgressCode)?.Label,
                StartDate = project.StartDate, PlannedFinishDate = project.PlannedFinishDate,
                ClosedDate = project.ClosedDate, OwnerId = project.OwnerId,
                OwnerName = project.OwnerId.HasValue ? users.GetValueOrDefault(project.OwnerId.Value) : null,
                TestStatus = project.TestStatus, IsDeleted = project.IsDeleted,
                FollowUpIntervalDays = project.FollowUpIntervalDays,
                NextFollowUpDueDate = nextFollowup,
                FollowUpStatus = nextFollowup < BusinessClock.Today ? "已逾期" : nextFollowup == BusinessClock.Today ? "今日到期" : "未到期",
                MaterialCount = materials.Count(x => !x.IsDeleted), CreatedAt = project.CreatedAt,
                RowVersion = project.RowVersion
            },
            Materials = materials.Select(x => new TestMaterialDto
            {
                Id = x.Id, ProjectId = project.Id, ProjectName = project.Name,
                Name = x.Name, MaterialNo = x.MaterialNo, VendorName = x.VendorName,
                DockingVendorName = x.DockingVendorName, Model = x.Model, Brand = x.Brand,
                Quantity = x.Quantity, Status = x.Status, IsDeleted = x.IsDeleted,
                DepartmentName = x.DepartmentId.HasValue ? depts.GetValueOrDefault(x.DepartmentId.Value) : null,
                CustodianName = x.CustodianId.HasValue ? users.GetValueOrDefault(x.CustodianId.Value) : null,
                LocationName = x.LocationName, ReceivedDate = x.ReceivedDate, Remark = x.Remark
            }).ToList(),
            Stages = (await _db.ProjectTestStages.Where(x => x.ProjectId == project.Id && !x.IsDeleted &&
                (!stageId.HasValue || x.Id == stageId)).OrderBy(x => x.Sequence).ToListAsync()).Select(StageDto).ToList(),
            Documents = (await _db.ProjectReportDocuments.Where(x => x.ProjectId == project.Id && !x.IsDeleted &&
                (!stageId.HasValue || x.StageId == stageId)).OrderBy(x => x.UploadedAt).ToListAsync()).Select(DocumentDto).ToList(),
            Followups = stageId.HasValue ? new() : await _db.TestProjectFollowups
                .Where(x => x.ProjectId == project.Id).OrderBy(x => x.DueDate)
                .Select(x => new TestProjectFollowupDto
                {
                    Id = x.Id, ProjectId = x.ProjectId, Content = x.Content,
                    DueDate = x.DueDate, FilledAt = x.FilledAt, FilledById = x.FilledById,
                    FilledByName = _db.Users.Where(u => u.Id == x.FilledById).Select(u => u.Name).FirstOrDefault()
                }).ToListAsync(),
            Flows = stageId.HasValue ? new() : await _db.MaterialFlows
                .Where(x => materialIds.Contains(x.MaterialId)).OrderBy(x => x.ApplyTime).Take(10001)
                .Select(x => new MaterialFlowDto
                {
                    Id = x.Id, FlowNo = x.FlowNo, ProjectId = project.Id, MaterialName = x.MaterialName,
                    MaterialNo = x.MaterialNo, Applicant = x.Applicant, Transferee = x.Transferee,
                    Reason = x.Reason, ApplyTime = DateTime.SpecifyKind(x.ApplyTime, DateTimeKind.Utc),
                    Status = x.Status, Deadline = DateTime.SpecifyKind(x.Deadline, DateTimeKind.Utc),
                    DirectTransfer = x.DirectTransfer
                }).ToListAsync(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static async Task AddEntryAsync(ZipArchive zip, string name, Stream source)
    {
        await using (source)
        await using (var target = zip.CreateEntry(name, CompressionLevel.Fastest).Open())
            await source.CopyToAsync(target);
    }

    private async Task<TestProject> LoadProjectAsync(int projectId) =>
        await _db.TestProjects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId)
        ?? throw new BizException(4046, "测试项目不存在");

    private async Task<TestProject> LockWritableProjectAsync(int projectId, int userId)
    {
        var project = await _db.TestProjects
            .FromSqlInterpolated($"SELECT * FROM test_projects WHERE Id = {projectId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync() ?? throw new BizException(4046, "测试项目不存在");
        await EnsureWritableAsync(project, userId);
        return project;
    }

    private async Task<bool> CanManageAsync(TestProject project, int userId) =>
        await _accessScope.CanExecuteAsync(project, userId, "project:manage", true);

    private async Task EnsureWritableAsync(TestProject project, int userId)
    {
        if (project.IsDeleted) throw new BizException(4092, "已删除项目为只读，不能修改测试阶段或报告");
        if (!await CanMaintainStagesAsync(project))
            throw new BizException(4092, "项目只有进入测试中后才能维护测试阶段与报告，落地跟进和已结案项目已锁定");
        if (!await CanManageAsync(project, userId))
            throw new BizException(4031, "只有项目负责人、项目管理人员或管理员可以维护测试阶段和报告");
    }

    private static bool IsTestingMaintainable(TestProject project) =>
        TestProjectLifecycleRules.IsTestingStageMaintainable(
            project.ProgressCode,
            project.ClosedDate);

    /// <summary>
    /// 测试中可以维护阶段。已经标成落地跟进、但阶段仍未完成或没有结束日期时，也允许补齐。
    /// </summary>
    private async Task<bool> CanMaintainStagesAsync(TestProject project)
    {
        if (project.IsDeleted || project.ClosedDate.HasValue) return false;
        if (IsTestingMaintainable(project)) return true;
        if (!string.Equals(
                project.ProgressCode,
                TestProjectLifecycleRules.Landing,
                StringComparison.OrdinalIgnoreCase))
            return false;
        return await _db.ProjectTestStages.AnyAsync(x =>
            x.ProjectId == project.Id &&
            !x.IsDeleted &&
            (x.EndDate == null ||
             x.TestContent == null || x.TestContent.Trim() == "" ||
             x.TestResult == null || x.TestResult.Trim() == ""));
    }

    private async Task CheckStageAsync(int projectId, int? stageId)
    {
        if (stageId.HasValue && !await _db.ProjectTestStages.AnyAsync(x => x.Id == stageId && x.ProjectId == projectId && !x.IsDeleted))
            throw new BizException(4046, "测试阶段不存在或不属于当前项目");
    }

    internal static void ValidateStage(
        SaveProjectTestStageRequest request,
        DateTime? projectStartDate = null)
    {
        if (request.Sequence is < 1 or > 999) throw new BizException(4001, "阶段序号必须为 1–999");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
            throw new BizException(4001, "请填写阶段名称，最多 100 个字符");
        if (request.Objective?.Length > 2000 ||
            request.TestContent?.Length > 8000 || request.TestResult?.Length > 8000)
            throw new BizException(4001, "阶段目标最多 2000 字，测试内容/结果最多 8000 字");
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value.Date < request.StartDate.Value.Date)
            throw new BizException(4001, "结束日期不能早于开始日期");
        if (projectStartDate.HasValue &&
            ((request.StartDate.HasValue && request.StartDate.Value.Date < projectStartDate.Value.Date) ||
             (request.EndDate.HasValue && request.EndDate.Value.Date < projectStartDate.Value.Date)))
            throw new BizException(4001, "测试阶段日期不能早于项目开始日期");
        if (request.EndDate.HasValue)
        {
            if (!request.StartDate.HasValue ||
                string.IsNullOrWhiteSpace(request.TestContent) ||
                string.IsNullOrWhiteSpace(request.TestResult))
                throw new BizException(4001, "填写结束日期前请同时填写开始日期、测试内容和测试结果");
            if (request.EndDate.Value.Date > BusinessClock.Today)
                throw new BizException(4001, "结束日期不能晚于今天");
        }
    }

    private async Task SaveChangesAsync()
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new BizException(4090, "阶段已被更新，请刷新后重试"); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 } mysql)
        {
            if (mysql.Message.Contains("ActiveSequence", StringComparison.OrdinalIgnoreCase))
                throw new BizException(4094, "该阶段序号已存在，请使用其他序号");
            throw new BizException(4092, "请先填写当前阶段的结束日期");
        }
    }

    internal static string DeriveStageStatus(DateTime? startDate, DateTime? endDate)
    {
        if (endDate.HasValue) return "completed";
        if (startDate.HasValue) return "in_progress";
        return "planned";
    }

    private static bool IsCurrentStage(DateTime? startDate, DateTime? endDate) =>
        startDate.HasValue && !endDate.HasValue;

    private static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static ProjectTestStageDto StageDto(ProjectTestStage x) => new()
    {
        Id = x.Id, ProjectId = x.ProjectId, Sequence = x.Sequence, Name = x.Name,
        Objective = x.Objective, TestContent = x.TestContent, TestResult = x.TestResult,
        StartDate = x.StartDate, EndDate = x.EndDate,
        Status = x.Status, RowVersion = x.RowVersion, UpdatedAt = DateTime.SpecifyKind(x.UpdatedAt, DateTimeKind.Utc)
    };

    private static ProjectReportDocumentDto DocumentDto(ProjectReportDocument x) => new()
    {
        Id = x.Id, ProjectId = x.ProjectId, StageId = x.StageId, FileName = x.FileName,
        ContentType = x.ContentType, Size = x.Size, Description = x.Description,
        UploadedBy = x.UploadedBy, UploadedAt = DateTime.SpecifyKind(x.UploadedAt, DateTimeKind.Utc)
    };
}
