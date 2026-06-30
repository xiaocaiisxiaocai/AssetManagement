using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssetManagement.Application.Audit;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Reports;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Reports;

public class ReportApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ReportApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Summary_aggregates_by_category_and_department()
    {
        await Login();
        var category = await CreateCategory();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = 1,
            Name = "报表部门"
        });
        await CreateAsset(category.Id, department.Data!.Id, "报表资产A", AssetStatus.Available);
        await CreateAsset(category.Id, department.Data.Id, "报表资产B", AssetStatus.Borrowed);

        var summary = await _client.GetFromJsonAsync<ApiResult<AssetSummaryDto>>("/api/reports/summary");

        summary!.Data!.Total.Should().BeGreaterThanOrEqualTo(2);
        summary.Data.ByCategory.Should().Contain(x => x.CategoryCode == category.Code && x.Total >= 2);
        summary.Data.ByDept.Should().Contain(x => x.DepartmentName == "报表部门" && x.Total >= 2);
    }

    [Fact]
    public async Task Summary_does_not_expose_maintenance_or_scrapped_counts()
    {
        await Login();

        var json = await _client.GetStringAsync("/api/reports/summary");
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("maintenance", out _).Should().BeFalse();
        data.TryGetProperty("scrapped", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Borrowed_report_reads_approved_borrow_flow()
    {
        await Login();
        var category = await CreateCategory();
        var asset = await CreateAsset(category.Id, null, "借用报表资产", AssetStatus.Available);
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "报表验证",
            ReturnDate = "2026-06-20"
        });

        // 确保流程启动成功
        flow.Should().NotBeNull();
        flow.Data.Should().NotBeNull();

        // 审批（BPMN 模式下，一次审批应该完成流程，默认流程）
        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve", new ApprovalActionRequest { Opinion = "同意" });
        approved.Data.Should().NotBeNull();
        approved.Data!.Status.Should().Be("approved", "流程应该已完成");

        var borrowed = await _client.GetFromJsonAsync<ApiResult<PagedResult<BorrowReportRow>>>("/api/reports/borrowed");

        borrowed!.Data!.Items.Should().Contain(x => x.AssetId == asset.Id && x.FlowId == flow.Data.Id);
    }

    [Fact]
    public async Task Overdue_report_and_remind_creates_notification()
    {
        await Login();
        var category = await CreateCategory();
        var asset = await CreateAsset(category.Id, null, "逾期资产", AssetStatus.Available);
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "逾期验证",
            ReturnDate = "2020-01-01"
        });

        // 确保流程启动成功
        flow.Should().NotBeNull();
        flow.Data.Should().NotBeNull();

        // 审批完成流程
        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve", new ApprovalActionRequest { Opinion = "同意" });
        approved.Data.Should().NotBeNull();
        approved.Data!.Status.Should().Be("approved", "流程应该已完成");

        var overdue = await _client.GetFromJsonAsync<ApiResult<List<OverdueReportRow>>>("/api/reports/overdue");
        await Post<ApiResult<object?>>($"/api/reports/overdue/{asset.Id}/remind", new { });
        var audit = await _client.GetFromJsonAsync<ApiResult<PagedResult<AuditLogDto>>>("/api/audit-logs?actionType=remind");

        overdue!.Data!.Should().Contain(x => x.AssetId == asset.Id && x.OverdueDays > 0);
        audit!.Data!.Items.Should().Contain(x => x.TargetId == asset.Id.ToString());
    }

    [Fact]
    public async Task Audit_cleanup_preview_and_delete_only_support_allowed_retention_days()
    {
        await Login();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogs.AddRange(
            new AuditLog { ActionType = "POST", TargetType = "Test", Summary = "old-10", OccurredAt = DateTime.Now.AddDays(-10) },
            new AuditLog { ActionType = "POST", TargetType = "Test", Summary = "old-20", OccurredAt = DateTime.Now.AddDays(-20) },
            new AuditLog { ActionType = "POST", TargetType = "Test", Summary = "new-3", OccurredAt = DateTime.Now.AddDays(-3) });
        await db.SaveChangesAsync();

        var preview = await _client.GetFromJsonAsync<ApiResult<AuditCleanupPreviewDto>>("/api/audit-logs/cleanup-preview?retentionDays=7");
        preview!.Data!.RetentionDays.Should().Be(7);
        preview.Data.DeleteCount.Should().BeGreaterThanOrEqualTo(2);

        var invalid = await _client.DeleteAsync("/api/audit-logs?retentionDays=10");
        invalid.EnsureSuccessStatusCode();
        var invalidBody = await invalid.Content.ReadFromJsonAsync<ApiResult<object>>();
        invalidBody!.Code.Should().NotBe(0);

        var cleanup = await _client.DeleteAsync("/api/audit-logs?retentionDays=7");
        cleanup.EnsureSuccessStatusCode();
        var body = await cleanup.Content.ReadFromJsonAsync<ApiResult<AuditCleanupResultDto>>();
        body!.Data!.DeletedCount.Should().BeGreaterThanOrEqualTo(2);
        db.ChangeTracker.Clear();
        db.AuditLogs.Any(x => x.Summary == "new-3").Should().BeTrue();
        db.AuditLogs.Any(x => x.Summary == "old-10" || x.Summary == "old-20").Should().BeFalse();
        db.AuditLogs.Any(x => x.ActionType == "cleanup" && x.TargetType == "AuditLog").Should().BeTrue();
    }

    [Fact]
    public async Task Database_backups_lists_existing_backup_files()
    {
        await Login();
        var backupDir = Path.Combine(Path.GetTempPath(), "assetmgmt-backup-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupDir);
        var olderFile = Path.Combine(backupDir, "assetmgmt_20260629_020000.sql");
        var newerFile = Path.Combine(backupDir, "assetmgmt_20260630_020000.sql");
        await File.WriteAllTextAsync(olderFile, "old");
        await File.WriteAllTextAsync(newerFile, "newer");
        File.SetLastWriteTime(olderFile, new DateTime(2026, 6, 29, 2, 0, 0));
        File.SetLastWriteTime(newerFile, new DateTime(2026, 6, 30, 2, 0, 0));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.SystemSettings
            .Where(x => x.Key == "database_backup_path")
            .ExecuteUpdateAsync(x => x.SetProperty(setting => setting.Value, backupDir));

        var result = await _client.GetFromJsonAsync<ApiResult<List<DatabaseBackupFileDto>>>("/api/database-backups");

        result!.Data!.Should().HaveCount(2);
        result.Data.Select(x => x.FileName).Should().Equal(
            "assetmgmt_20260630_020000.sql",
            "assetmgmt_20260629_020000.sql");
        result.Data[0].SizeBytes.Should().Be(5);
        result.Data[0].FilePath.Should().Be(newerFile);
    }

    private async Task<CategoryNodeDto> CreateCategory()
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("RP")
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = Unique("LEAF")
        });
        return child.Data!;
    }

    private async Task<AssetDto> CreateAsset(int categoryId, int? departmentId, string name, AssetStatus status)
    {
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = name,
            CategoryId = categoryId,
            DepartmentId = departmentId,
        });
        if (status == AssetStatus.Available)
        {
            return created.Data!;
        }

        var updated = await Put<ApiResult<AssetDto>>($"/api/assets/{created.Data!.Id}", new UpdateAssetRequest
        {
            Name = created.Data.Name,
            CategoryId = categoryId,
            DepartmentId = departmentId,
            Quantity = 1,
            Status = status
        });
        return updated.Data!;
    }

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];
}
