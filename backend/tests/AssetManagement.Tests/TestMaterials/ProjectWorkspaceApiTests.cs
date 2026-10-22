using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.TestMaterials;

public class ProjectWorkspaceApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    public ProjectWorkspaceApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Planning_project_cannot_create_testing_stage_until_testing_starts()
    {
        var (client, projectId, _) = await SetupAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == projectId);
            project.ProgressCode = "planning";
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            $"{Root(projectId)}/stages", Draft("计划中不应新增", 1));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WorkspaceAsync(client, projectId)).Stages.Should().BeEmpty();
    }

    [Fact]
    public async Task Planning_project_cannot_modify_existing_testing_stage_or_report()
    {
        var (client, projectId, _) = await SetupAsync();
        var stage = await CreateAsync(client, projectId, Draft("测试阶段", 1));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == projectId);
            project.ProgressCode = "planning";
            await db.SaveChangesAsync();
        }

        var update = await client.PutAsJsonAsync(
            $"{Root(projectId)}/stages/{stage.Id}",
            Draft("计划中不应编辑", 1));
        using var report = PdfForm(stage.Id, "计划中不应上传.pdf");
        var upload = await client.PostAsync($"{Root(projectId)}/documents", report);

        update.StatusCode.Should().Be(HttpStatusCode.Conflict);
        upload.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Owner_can_plan_update_and_read_persisted_stages_with_complete_test_results()
    {
        var (client, projectId, _) = await SetupAsync();
        var created = await CreateAsync(client, projectId, Draft("第一阶段：耐久测试", 1));
        var complete = Draft(created.Name, 1);
        complete.Status = "completed";
        complete.StartDate = BusinessClock.Today.AddDays(-2);
        complete.EndDate = BusinessClock.Today;
        complete.TestContent = "连续负载运行 48 小时\n记录温升与重复定位误差";
        complete.TestResult = "温升 15℃，重复定位误差 0.02mm";
        complete.RowVersion = created.RowVersion;
        var response = await client.PutAsJsonAsync($"{Root(projectId)}/stages/{created.Id}", complete);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<ApiResult<ProjectTestStageDto>>();
        saved!.Data!.RowVersion.Should().BeGreaterThan(created.RowVersion);

        var workspace = await WorkspaceAsync(client, projectId);
        workspace.CanManage.Should().BeTrue();
        workspace.Stages.Should().ContainSingle().Which.TestResult.Should().Be(complete.TestResult);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs.Where(x => x.TargetType == "ProjectWorkspace" &&
            x.TargetId == created.Id.ToString() && x.ActionType == "PUT").OrderByDescending(x => x.Id).FirstAsync();
        audit.Detail.Should().Contain("TestResult").And.Contain(complete.TestResult);
    }

    [Fact]
    public async Task Landing_transition_requires_final_stage_report_and_locks_new_testing_writes()
    {
        var (client, projectId, _) = await SetupAsync();
        var rowVersion = await ProjectVersionAsync(projectId);
        var landingRequest = new
        {
            ProgressCode = "landing",
            ClosedDate = (DateTime?)null,
            TestStatus = "待进入落地跟进",
            RowVersion = rowVersion
        };
        var blocked = await client.PutAsJsonAsync(
            $"/api/test-projects/{projectId}/progress",
            landingRequest);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var stage = await CreateAsync(client, projectId, new SaveProjectTestStageRequest
        {
            Name = "结案阶段",
            Sequence = 1,
            StartDate = BusinessClock.Today.AddDays(-1),
            EndDate = BusinessClock.Today,
            Status = "completed",
            TestContent = "完成最终验证",
            TestResult = "验证通过"
        });
        using var report = PdfForm(stage.Id, "结案阶段报告.pdf");
        (await client.PostAsync($"{Root(projectId)}/documents", report)).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/test-projects/{projectId}/progress", landingRequest))
            .EnsureSuccessStatusCode();
        var workspace = await WorkspaceAsync(client, projectId);
        workspace.CanManage.Should().BeFalse();
        workspace.CanExport.Should().BeTrue();

        (await client.PostAsJsonAsync($"{Root(projectId)}/stages", Draft("不应新增", 2)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var secondReport = PdfForm(stage.Id, "不应上传.pdf");
        (await client.PostAsync($"{Root(projectId)}/documents", secondReport))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Landing_transition_rejects_incomplete_earlier_stage()
    {
        var (client, projectId, _) = await SetupAsync();
        var rowVersion = await ProjectVersionAsync(projectId);
        await CreateAsync(client, projectId, Draft("前置阶段", 1));
        var finalStage = await CreateAsync(client, projectId, new SaveProjectTestStageRequest
        {
            Name = "最终阶段",
            Sequence = 2,
            StartDate = BusinessClock.Today.AddDays(-1),
            EndDate = BusinessClock.Today,
            Status = "completed",
            TestContent = "完成最终验证",
            TestResult = "验证通过"
        });
        using var report = PdfForm(finalStage.Id, "最终阶段报告.pdf");
        (await client.PostAsync($"{Root(projectId)}/documents", report)).EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"/api/test-projects/{projectId}/progress", new
        {
            ProgressCode = "landing",
            ClosedDate = (DateTime?)null,
            TestStatus = "前置阶段仍未完成",
            RowVersion = rowVersion
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stage_updates_reject_stale_versions_without_overwriting_saved_results()
    {
        var (client, projectId, _) = await SetupAsync();
        var created = await CreateAsync(client, projectId, Draft("并发阶段", 1));
        var update = Draft("第一位编辑者", 1);
        update.RowVersion = created.RowVersion;
        (await client.PutAsJsonAsync($"{Root(projectId)}/stages/{created.Id}", update)).EnsureSuccessStatusCode();
        update.Name = "旧版本不应覆盖";
        var stale = await client.PutAsJsonAsync($"{Root(projectId)}/stages/{created.Id}", update);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WorkspaceAsync(client, projectId)).Stages.Single().Name.Should().Be("第一位编辑者");
    }

    [Fact]
    public async Task Duplicate_sequences_and_multiple_current_stages_are_rejected()
    {
        var (client, projectId, _) = await SetupAsync();
        var current = Draft("当前测试", 1);
        current.Status = "in_progress";
        current.StartDate = BusinessClock.Today;
        await CreateAsync(client, projectId, current);
        var duplicate = await client.PostAsJsonAsync($"{Root(projectId)}/stages", Draft("重复序号", 1));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        current.Sequence = 2;
        current.Status = "blocked";
        var second = await client.PostAsJsonAsync($"{Root(projectId)}/stages", current);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WorkspaceAsync(client, projectId)).Stages.Should().ContainSingle();
    }

    [Theory]
    [InlineData("completed", true, true)]
    [InlineData("in_progress", false, false)]
    [InlineData("invalid", false, false)]
    public async Task Invalid_status_or_missing_completion_evidence_does_not_save(string status, bool hasStart, bool hasEnd)
    {
        var (client, projectId, _) = await SetupAsync();
        var request = Draft("不完整测试", 1);
        request.Status = status;
        request.StartDate = hasStart ? BusinessClock.Today : null;
        request.EndDate = hasEnd ? BusinessClock.Today : null;
        var response = await client.PostAsJsonAsync($"{Root(projectId)}/stages", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WorkspaceAsync(client, projectId)).Stages.Should().BeEmpty();
    }

    [Fact]
    public async Task Reversed_dates_are_rejected_and_completed_stages_cannot_end_in_future()
    {
        var (client, projectId, _) = await SetupAsync();
        var request = Draft("时间范围", 1);
        request.StartDate = BusinessClock.Today;
        request.EndDate = BusinessClock.Today.AddDays(-1);
        (await client.PostAsJsonAsync($"{Root(projectId)}/stages", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        request.EndDate = BusinessClock.Today.AddDays(1);
        request.TestContent = "测试内容";
        request.TestResult = "测试结果";
        request.Status = "completed";
        (await client.PostAsJsonAsync($"{Root(projectId)}/stages", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stage_dates_cannot_precede_project_start_date()
    {
        var (client, projectId, _) = await SetupAsync();
        var request = Draft("早于项目开始的阶段", 1);
        request.Status = "completed";
        request.StartDate = BusinessClock.Today.AddDays(-8);
        request.EndDate = BusinessClock.Today.AddDays(-6);
        request.TestContent = "测试内容";
        request.TestResult = "测试结果";

        var response = await client.PostAsJsonAsync($"{Root(projectId)}/stages", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WorkspaceAsync(client, projectId)).Stages.Should().BeEmpty();
    }

    [Fact]
    public async Task Close_date_cannot_precede_any_completed_stage_end_date()
    {
        var (client, projectId, _) = await SetupAsync();
        await CreateAsync(client, projectId, new SaveProjectTestStageRequest
        {
            Name = "后结束的阶段", Sequence = 1, Status = "completed",
            StartDate = BusinessClock.Today.AddDays(-3), EndDate = BusinessClock.Today,
            TestContent = "测试内容", TestResult = "通过"
        });
        var finalStage = await CreateAsync(client, projectId, new SaveProjectTestStageRequest
        {
            Name = "序号最大的阶段", Sequence = 2, Status = "completed",
            StartDate = BusinessClock.Today.AddDays(-4), EndDate = BusinessClock.Today.AddDays(-2),
            TestContent = "测试内容", TestResult = "通过"
        });
        using var report = PdfForm(finalStage.Id, "最终阶段报告.pdf");
        (await client.PostAsync($"{Root(projectId)}/documents", report)).EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"/api/test-projects/{projectId}/progress", new
        {
            ProgressCode = "closed",
            ClosedDate = BusinessClock.Today.AddDays(-1),
            TestStatus = "不应提前结案",
            RowVersion = await ProjectVersionAsync(projectId)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Viewer_cannot_modify_stages_upload_reports_or_export_another_project()
    {
        var (_, projectId, _) = await SetupAsync();
        var viewer = await EmployeeClientAsync();
        (await WorkspaceAsync(viewer, projectId)).CanManage.Should().BeFalse();
        (await viewer.PostAsJsonAsync($"{Root(projectId)}/stages", Draft("不应保存", 1)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var upload = PdfForm();
        (await viewer.PostAsync($"{Root(projectId)}/documents", upload)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync($"{Root(projectId)}/report")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (owner, ownedProjectId, _) = await SetupAsync();
        var stage = await CreateAsync(owner, ownedProjectId, Draft("原件阶段", 1));
        using var ownedReport = PdfForm(stage.Id, "原件报告.pdf");
        var uploaded = await owner.PostAsync($"{Root(ownedProjectId)}/documents", ownedReport);
        uploaded.EnsureSuccessStatusCode();
        var document = (await uploaded.Content.ReadFromJsonAsync<ApiResult<ProjectReportDocumentDto>>())!.Data!;
        (await viewer.GetAsync($"{Root(ownedProjectId)}/documents/{document.Id}/download"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.GetAsync($"{Root(ownedProjectId)}/documents/{document.Id}/download"))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Progress_cannot_move_backward_or_skip_from_planning()
    {
        var (client, projectId, _) = await SetupAsync();
        var backward = await client.PutAsJsonAsync($"/api/test-projects/{projectId}/progress", new
        {
            ProgressCode = "planning",
            RowVersion = await ProjectVersionAsync(projectId)
        });
        backward.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await backward.Content.ReadFromJsonAsync<ApiResult<object>>())!.Message.Should().Contain("不能回退");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == projectId);
            project.ProgressCode = "planning";
            await db.SaveChangesAsync();
        }

        var skipped = await client.PutAsJsonAsync($"/api/test-projects/{projectId}/progress", new
        {
            ProgressCode = "closed",
            ClosedDate = BusinessClock.Today,
            RowVersion = await ProjectVersionAsync(projectId)
        });
        skipped.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await skipped.Content.ReadFromJsonAsync<ApiResult<object>>())!.Message
            .Should().Contain("计划中的项目只能保持计划中或进入测试中");
    }

    [Fact]
    public async Task Admin_can_reopen_closed_project_but_supervisor_cannot()
    {
        var (_, projectId, _) = await SetupAsync();
        uint version;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supervisorRole = await db.Roles.SingleAsync(x => x.Code == "supervisor");
            var department = new Department { Code = $"RW-{Guid.NewGuid():N}"[..12], Name = "重开部门", IsActive = true };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
            var supervisor = new User
            {
                EmployeeNo = $"RW-{Guid.NewGuid():N}"[..12],
                Name = "重开主管",
                DepartmentId = department.Id,
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
            };
            db.Users.Add(supervisor);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id });
            var project = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == projectId);
            project.ProgressCode = "closed";
            project.ClosedDate = BusinessClock.Today;
            await db.SaveChangesAsync();
            version = project.RowVersion;
            var supervisorClient = _factory.CreateClient();
            var login = await supervisorClient.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest { EmployeeNo = supervisor.EmployeeNo, Password = "123456" });
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!.Data!.Token;
            supervisorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            (await supervisorClient.PostAsJsonAsync(
                $"/api/test-projects/{projectId}/reopen", new { RowVersion = version }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var admin = _factory.CreateClient();
        var adminLogin = await admin.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { EmployeeNo = "1001", Password = "123456" });
        adminLogin.EnsureSuccessStatusCode();
        var adminToken = (await adminLogin.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!.Data!.Token;
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reopened = await admin.PostAsJsonAsync(
            $"/api/test-projects/{projectId}/reopen", new { RowVersion = version });
        reopened.EnsureSuccessStatusCode();
        var body = await reopened.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();
        body!.Data!.ProgressCode.Should().Be("testing");
        body.Data.ClosedDate.Should().BeNull();
    }

    [Fact]
    public async Task Stage_reports_and_project_final_reports_are_downloadable_and_packaged_with_full_process()
    {
        var (client, projectId, _) = await SetupAsync();
        var first = await CreateAsync(client, projectId, Draft("样机性能验证", 1));
        var second = await CreateAsync(client, projectId, Draft("整机联调验证", 2));
        using var stageForm = PdfForm(first.Id, "性能验证报告.pdf");
        var uploaded = await client.PostAsync($"{Root(projectId)}/documents", stageForm);
        uploaded.EnsureSuccessStatusCode();
        var document = (await uploaded.Content.ReadFromJsonAsync<ApiResult<ProjectReportDocumentDto>>())!.Data!;
        using var finalForm = PdfForm(second.Id, "项目最终报告.pdf");
        (await client.PostAsync($"{Root(projectId)}/documents", finalForm)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"{Root(projectId)}/documents/{document.Id}/download");
        download.EnsureSuccessStatusCode();
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");

        var report = await client.GetAsync($"{Root(projectId)}/report");
        report.EnsureSuccessStatusCode();
        using (var docx = new ZipArchive(new MemoryStream(await report.Content.ReadAsByteArrayAsync())))
        {
            using var reader = new StreamReader(docx.GetEntry("word/document.xml")!.Open());
            var text = await reader.ReadToEndAsync();
            text.Should().Contain("样机性能验证").And.Contain("整机联调验证");
        }

        var response = await client.GetAsync($"{Root(projectId)}/package");
        response.EnsureSuccessStatusCode();
        using var package = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        package.GetEntry("测试报告.docx").Should().NotBeNull();
        package.GetEntry("测试报告.html").Should().NotBeNull();
        package.GetEntry($"documents/{document.Id}/性能验证报告.pdf").Should().NotBeNull();
        package.Entries.Count(x => x.FullName.StartsWith("documents/", StringComparison.Ordinal)).Should().Be(2);

        var stageResponse = await client.GetAsync($"{Root(projectId)}/package?stageId={first.Id}");
        stageResponse.EnsureSuccessStatusCode();
        using var stagePackage = new ZipArchive(new MemoryStream(await stageResponse.Content.ReadAsByteArrayAsync()));
        stagePackage.Entries.Count(x => x.FullName.StartsWith("documents/", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task Project_level_report_upload_is_rejected_without_stage_assignment()
    {
        var (client, projectId, _) = await SetupAsync();
        using var form = PdfForm(null, "不应作为项目总报告.pdf");
        (await client.PostAsync($"{Root(projectId)}/documents", form))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attachment_cannot_be_attached_to_or_downloaded_through_another_project()
    {
        var (client, projectId, ownerId) = await SetupAsync();
        var (_, otherProjectId, _) = await SetupAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var other = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == otherProjectId);
            other.OwnerId = ownerId;
            await db.SaveChangesAsync();
        }
        var stage = await CreateAsync(client, projectId, Draft("关联阶段", 1));
        using var wrongScope = PdfForm(stage.Id);
        (await client.PostAsync($"{Root(otherProjectId)}/documents", wrongScope)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var correctScope = PdfForm(stage.Id);
        var response = await client.PostAsync($"{Root(projectId)}/documents", correctScope);
        response.EnsureSuccessStatusCode();
        var document = (await response.Content.ReadFromJsonAsync<ApiResult<ProjectReportDocumentDto>>())!.Data!;
        (await client.GetAsync($"{Root(otherProjectId)}/documents/{document.Id}/download"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Spoofed_report_extensions_and_empty_uploads_are_rejected()
    {
        var (client, projectId, _) = await SetupAsync();
        var stage = await CreateAsync(client, projectId, Draft("文件校验阶段", 1));
        using var spoofed = new MultipartFormDataContent();
        spoofed.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<script>bad()</script>")), "file", "假报告.pdf");
        spoofed.Add(new StringContent(stage.Id.ToString()), "stageId");
        (await client.PostAsync($"{Root(projectId)}/documents", spoofed)).StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        using var executable = PdfForm(stage.Id, "report.exe");
        (await client.PostAsync($"{Root(projectId)}/documents", executable)).StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        using var empty = new MultipartFormDataContent();
        empty.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "empty.pdf");
        empty.Add(new StringContent(stage.Id.ToString()), "stageId");
        (await client.PostAsync($"{Root(projectId)}/documents", empty)).StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Deletion_preserves_report_archive_and_project_deleted_state_is_read_only()
    {
        var (client, projectId, ownerId) = await SetupAsync();
        var stage = await CreateAsync(client, projectId, Draft("历史阶段", 1));
        using var form = PdfForm(stage.Id);
        var uploaded = await client.PostAsync($"{Root(projectId)}/documents", form);
        uploaded.EnsureSuccessStatusCode();
        var document = (await uploaded.Content.ReadFromJsonAsync<ApiResult<ProjectReportDocumentDto>>())!.Data!;
        (await client.DeleteAsync($"{Root(projectId)}/stages/{stage.Id}?rowVersion={stage.RowVersion}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"{Root(projectId)}/documents/{document.Id}")).EnsureSuccessStatusCode();
        (await client.GetAsync($"{Root(projectId)}/documents/{document.Id}/download")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"{Root(projectId)}/stages/{stage.Id}?rowVersion={stage.RowVersion}")).EnsureSuccessStatusCode();
        (await WorkspaceAsync(client, projectId)).Stages.Should().BeEmpty();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.TestProjects.AsTracking().SingleAsync(x => x.Id == projectId);
            project.IsDeleted = true;
            await db.SaveChangesAsync();
        }
        (await WorkspaceAsync(client, projectId)).CanManage.Should().BeFalse();
        (await client.PostAsJsonAsync($"{Root(projectId)}/stages", Draft("不应保存", 2)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_stage_transitions_cannot_create_two_current_stages()
    {
        var (client, projectId, _) = await SetupAsync();
        var first = Draft("并行测试一", 1);
        first.Status = "in_progress";
        first.StartDate = BusinessClock.Today;
        var second = Draft("并行测试二", 2);
        second.Status = "in_progress";
        second.StartDate = BusinessClock.Today;
        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"{Root(projectId)}/stages", first),
            client.PostAsJsonAsync($"{Root(projectId)}/stages", second));
        results.Count(x => x.IsSuccessStatusCode).Should().Be(1);
        results.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        (await WorkspaceAsync(client, projectId)).Stages.Should().ContainSingle();
    }

    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\nproject test report\n%%EOF");
    private static string Root(int id) => $"/api/test-projects/{id}/workspace";
    private static SaveProjectTestStageRequest Draft(string name, int sequence) => new() { Name = name, Sequence = sequence };

    private static MultipartFormDataContent PdfForm(int? stageId = null, string fileName = "报告.pdf")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(PdfBytes), "file", fileName);
        if (stageId.HasValue) form.Add(new StringContent(stageId.Value.ToString()), "stageId");
        form.Add(new StringContent("本阶段验证记录"), "description");
        return form;
    }

    private static async Task<ProjectTestStageDto> CreateAsync(HttpClient client, int projectId, SaveProjectTestStageRequest request)
    {
        var response = await client.PostAsJsonAsync($"{Root(projectId)}/stages", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<ProjectTestStageDto>>())!.Data!;
    }

    private static async Task<ProjectWorkspaceDto> WorkspaceAsync(HttpClient client, int projectId)
    {
        var response = await client.GetFromJsonAsync<ApiResult<ProjectWorkspaceDto>>(Root(projectId));
        return response!.Data!;
    }

    private async Task<uint> ProjectVersionAsync(int projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TestProjects.Where(x => x.Id == projectId).Select(x => x.RowVersion).SingleAsync();
    }

    private async Task<(HttpClient Client, int ProjectId, int OwnerId)> SetupAsync()
    {
        var client = await EmployeeClientAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var login = client.DefaultRequestHeaders.GetValues("X-Test-Employee").Single();
        client.DefaultRequestHeaders.Remove("X-Test-Employee");
        var owner = await db.Users.SingleAsync(x => x.EmployeeNo == login);
        var project = new TestProject
        {
            Name = $"阶段测试-{Guid.NewGuid():N}", Code = $"ST-{Guid.NewGuid():N}"[..32], OwnerId = owner.Id,
            ProgressCode = "testing", StartDate = BusinessClock.Today.AddDays(-5),
            PlannedFinishDate = BusinessClock.Today.AddDays(10), CreatedAt = DateTime.UtcNow
        };
        db.TestProjects.Add(project);
        await db.SaveChangesAsync();
        return (client, project.Id, owner.Id);
    }

    private async Task<HttpClient> EmployeeClientAsync()
    {
        var number = $"WS-{Guid.NewGuid():N}"[..20];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User { EmployeeNo = number, Name = "测试项目负责人", IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456") };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var role = await db.Roles.SingleAsync(x => x.Code == "employee");
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { EmployeeNo = number, Password = "123456" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!.Data!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Test-Employee", number);
        return client;
    }
}
