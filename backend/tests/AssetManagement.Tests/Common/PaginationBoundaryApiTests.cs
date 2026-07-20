using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Audit;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.Reports;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Common;

public class PaginationBoundaryApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public PaginationBoundaryApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Extreme_page_returns_empty_items_without_overflowing_to_earlier_data()
    {
        await SeedRowsForEveryPagedModule();
        await Login();

        await AssertExtremePage<AssetDto>("/api/assets");
        await AssertExtremePage<TestProjectDto>("/api/test-projects/page");
        await AssertExtremePage<TestMaterialDto>("/api/test-materials");
        await AssertExtremePage<BorrowReportRow>("/api/reports/borrowed");
        await AssertExtremePage<AuditLogDto>("/api/audit-logs");
        await AssertExtremePage<UserDto>("/api/users");
        await AssertExtremePage<UserOptionDto>("/api/users/options");
        await AssertExtremePage<RoleDto>("/api/roles");
    }

    [Fact]
    public async Task Invalid_page_and_page_size_are_normalized_to_public_contract()
    {
        await Login();

        var result = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            "/api/users?page=-2147483648&pageSize=2147483647");

        result!.Code.Should().Be(0);
        result.Data!.Page.Should().Be(1);
        result.Data.PageSize.Should().Be(AppConstants.MaxPageSize);
        result.Data.Items.Should().HaveCountLessThanOrEqualTo(AppConstants.MaxPageSize);
    }

    private async Task AssertExtremePage<T>(string path)
    {
        var separator = path.Contains('?') ? '&' : '?';
        var response = await _client.GetAsync(
            $"{path}{separator}page={int.MaxValue}&pageSize={AppConstants.MaxPageSize}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<T>>>();

        result!.Code.Should().Be(0, path);
        result.Data!.Total.Should().BePositive(path);
        result.Data.Page.Should().Be(int.MaxValue, path);
        result.Data.PageSize.Should().Be(AppConstants.MaxPageSize, path);
        result.Data.Items.Should().BeEmpty(path);
    }

    private async Task SeedRowsForEveryPagedModule()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var admin = await db.Users.SingleAsync(x => x.EmployeeNo == "1001");
        var workflow = await db.Workflows.SingleAsync(x => x.BizType == "borrow" && x.IsActive);

        var category = new AssetCategory
        {
            CodeSeg = $"PB{suffix[..6]}",
            Code = $"PB{suffix}"
        };
        var project = new TestProject
        {
            Name = $"分页边界项目-{suffix}",
            Code = $"PB-{suffix}",
            CreatedAt = DateTime.UtcNow
        };
        db.AddRange(category, project);
        await db.SaveChangesAsync();

        var asset = new Asset
        {
            AssetNo = $"PB-ASSET-{suffix}",
            Name = $"分页边界资产-{suffix}",
            CategoryId = category.Id,
            Status = AssetStatus.Borrowed,
            CreatedAt = DateTime.UtcNow
        };
        var material = new TestMaterial
        {
            MaterialNo = $"PB-MATERIAL-{suffix}",
            Name = $"分页边界料件-{suffix}",
            ProjectId = project.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.AddRange(asset, material);
        await db.SaveChangesAsync();

        db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = $"PB-FLOW-{suffix}",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = admin.Id,
            Applicant = admin.Name,
            Status = "approved",
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        db.AuditLogs.Add(new AuditLog
        {
            UserId = admin.Id,
            ActionType = "pagination_boundary",
            TargetType = "Pagination",
            TargetId = suffix,
            Summary = "分页边界回归测试",
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task Login()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>();
        body!.Code.Should().Be(0);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }
}

public class PaginationTests
{
    [Fact]
    public void GetOffset_uses_long_and_rejects_pages_beyond_int_range()
    {
        Pagination.GetOffset(int.MaxValue, AppConstants.MaxPageSize, int.MaxValue)
            .Should().BeNull();
        Pagination.GetOffset(int.MaxValue, 1, int.MaxValue)
            .Should().Be(int.MaxValue - 1);
    }
}
