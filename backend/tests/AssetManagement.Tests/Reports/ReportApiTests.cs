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
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Reports;

public class ReportApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public ReportApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Summary_aggregates_by_category_and_department()
    {
        await Login();
        var category = await CreateCategory();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
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
