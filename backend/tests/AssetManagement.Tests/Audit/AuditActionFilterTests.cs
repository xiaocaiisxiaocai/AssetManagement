using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Audit;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Audit;

public class AuditActionFilterTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuditActionFilterTests(TestWebAppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(AuditProbeController).Assembly);
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Successful_write_operation_creates_audit_log()
    {
        await Login();
        const string probePath = "/api/test-audit/write";
        using var beforeScope = _factory.Services.CreateScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = beforeDb.AuditLogs.Count(x => x.Summary.Contains(probePath));

        var res = await _client.PostAsJsonAsync(probePath, new { name = "demo" });

        res.EnsureSuccessStatusCode();
        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = afterDb.AuditLogs
            .Where(x => x.Summary.Contains(probePath))
            .OrderByDescending(x => x.Id)
            .First();
        afterDb.AuditLogs.Count(x => x.Summary.Contains(probePath)).Should().Be(before + 1);
        latest.ActionType.Should().Be("POST");
        latest.TargetType.Should().Be("AuditProbe");
        latest.Summary.Should().Contain(probePath);
    }

    [Fact]
    public async Task Audit_write_failure_does_not_turn_a_successful_business_response_into_failure()
    {
        await Login();

        var response = await _client.PostAsJsonAsync("/api/test-audit/audit-write-failure", new { });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResult<string>>();
        body!.Code.Should().Be(0);
        body.Data.Should().Be("business-succeeded");
    }

    [Fact]
    public async Task Failed_write_operation_creates_audit_log()
    {
        await Login();
        const string probePath = "/api/test-audit/fail";
        using var beforeScope = _factory.Services.CreateScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = beforeDb.AuditLogs.Count(x => x.Summary.Contains(probePath));

        var res = await _client.PostAsJsonAsync(probePath, new { name = "demo" });

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = afterDb.AuditLogs
            .Where(x => x.Summary.Contains(probePath))
            .OrderByDescending(x => x.Id)
            .First();
        afterDb.AuditLogs.Count(x => x.Summary.Contains(probePath)).Should().Be(before + 1);
        latest.ActionType.Should().Be("POST");
        latest.Detail.Should().Contain("\"success\":false");
        latest.Detail.Should().Contain("\"statusCode\":400");
    }

    [Fact]
    public async Task Asset_update_audit_log_records_target_id_and_change_detail()
    {
        await Login();
        var category = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "审计前名称",
            CategoryId = category.Data!.Id
        });

        var updated = await Put<ApiResult<AssetDto>>($"/api/assets/{created.Data!.Id}", new UpdateAssetRequest
        {
            Name = "审计后名称",
            CategoryId = category.Data.Id,
            Quantity = 1,
            Status = AssetStatus.Available
        });

        updated.Data!.Name.Should().Be("审计后名称");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs
            .Where(x => x.TargetType == "Asset" && x.TargetId == created.Data.Id.ToString())
            .OrderByDescending(x => x.Id)
            .First();
        latest.Detail.Should().Contain("\"before\"");
        latest.Detail.Should().Contain("\"after\"");
        latest.Detail.Should().Contain("\"changes\"");
        latest.Detail.Should().Contain("\"Name\"");
        latest.Detail.Should().Contain("审计前名称");
        latest.Detail.Should().Contain("审计后名称");
    }

    [Fact]
    public async Task Thrown_business_failure_is_audited_with_business_message()
    {
        await Login();
        const string probePath = "/api/test-audit/throw-biz";

        var response = await _client.PostAsJsonAsync(probePath, new { });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs.Where(x => x.Summary.Contains(probePath)).OrderByDescending(x => x.Id).First();
        latest.Detail.Should().Contain("业务校验失败");
        latest.Detail.Should().Contain("4092");
    }

    [Fact]
    public async Task Unexpected_exception_audit_does_not_store_sensitive_exception_message()
    {
        await Login();
        const string probePath = "/api/test-audit/throw-internal";

        await _client.PostAsJsonAsync(probePath, new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs.Where(x => x.Summary.Contains(probePath)).OrderByDescending(x => x.Id).First();
        latest.Detail.Should().Contain("服务器内部错误");
        latest.Detail.Should().NotContain("Server=secret-db");
    }

    [Fact]
    public async Task Permission_denied_write_is_audited_before_action_filter()
    {
        await Login();
        const string probePath = "/api/test-audit/denied";

        var response = await _client.PostAsJsonAsync(probePath, new { });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs.Where(x => x.Summary.Contains(probePath)).OrderByDescending(x => x.Id).First();
        latest.ActionType.Should().Be("POST_denied");
        latest.Detail.Should().Contain("4030");
    }

    [Fact]
    public async Task Settings_save_audit_log_records_changed_keys_and_values()
    {
        await Login();

        await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "page_size",
                Value = "42"
            }
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs
            .Where(x => x.TargetType == "Setting")
            .OrderByDescending(x => x.Id)
            .First();
        latest.Summary.Should().Contain("page_size");
        latest.Summary.Should().Contain("20");
        latest.Summary.Should().Contain("42");
        latest.Detail.Should().Contain("\"changes\"");
        latest.Detail.Should().Contain("page_size");
        latest.Detail.Should().Contain("20");
        latest.Detail.Should().Contain("42");
    }

    [Theory]
    [InlineData("/api/test-audit/roles/7/access", "配置角色授权", "部门主管", "权限数 3，菜单数 2")]
    [InlineData("/api/test-audit/roles/7/permissions", "分配角色权限", "部门主管", "权限数 3")]
    [InlineData("/api/test-audit/roles/7/menus", "分配角色菜单", "部门主管", "菜单数 2")]
    public async Task Role_assignment_audit_log_uses_business_summary(string url, string action, string roleName, string countText)
    {
        await Login();

        await Put<ApiResult<RoleDto>>(url, new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs
            .Where(x => x.Summary.Contains(action))
            .OrderByDescending(x => x.Id)
            .First();
        latest.Summary.Should().Contain(action);
        latest.Summary.Should().Contain(roleName);
        latest.Summary.Should().Contain(countText);
        latest.TargetId.Should().Be("7");
    }

    [Theory]
    [InlineData("/api/test-audit/approvals/9/approve", "审批通过")]
    [InlineData("/api/test-audit/approvals/9/reject", "审批驳回")]
    [InlineData("/api/test-audit/approvals/9/confirm-return", "确认归还")]
    public async Task Approval_action_audit_log_uses_business_summary(string url, string action)
    {
        await Login();

        await Post<ApiResult<ApprovalFlowDto>>(url, new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs
            .Where(x => x.Summary.Contains(action))
            .OrderByDescending(x => x.Id)
            .First();
        latest.Summary.Should().Contain(action);
        latest.Summary.Should().Contain("AF-TEST-001");
        latest.Summary.Should().Contain("A-001");
        latest.Summary.Should().Contain("测试资产");
        latest.TargetId.Should().Be("9");
    }

    [Fact]
    public async Task Asset_import_confirm_audit_log_uses_result_summary()
    {
        await Login();

        await Post<ApiResult<ImportConfirmResult>>("/api/test-audit/assets/import/confirm", new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs
            .Where(x => x.Summary.Contains("确认导入资产"))
            .OrderByDescending(x => x.Id)
            .First();
        latest.Summary.Should().Contain("成功 2 条");
        latest.Summary.Should().Contain("失败 1 条");
        latest.Summary.Should().Contain("电脑");
    }

    [Fact]
    public async Task User_import_audit_log_uses_result_summary()
    {
        await Login();

        await Post<ApiResult<UserImportResultDto>>("/api/test-audit/users/import", new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = db.AuditLogs
            .Where(x => x.Summary.Contains("导入用户"))
            .OrderByDescending(x => x.Id)
            .First();
        latest.Summary.Should().Contain("成功 3 条");
        latest.Summary.Should().Contain("失败 1 条");
        latest.Summary.Should().Contain("1001");
    }

    [Fact]
    public async Task Category_soft_delete_and_purge_use_distinct_audit_action_types()
    {
        await Login();
        var category = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });

        var softDelete = await _client.DeleteAsync($"/api/categories/{category.Data!.Id}");
        softDelete.EnsureSuccessStatusCode();
        var purge = await _client.DeleteAsync($"/api/categories/{category.Data.Id}/purge");
        purge.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = db.AuditLogs
            .Where(x => x.TargetType == "AssetCategory" && x.TargetId == category.Data.Id.ToString())
            .OrderBy(x => x.Id)
            .Select(x => x.ActionType)
            .ToList();
        actions.Should().ContainInOrder("POST", "soft_delete", "purge");
    }

    [Fact]
    public async Task Historical_delete_logs_are_classified_and_filterable()
    {
        await Login();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.AddRange(
                new AuditLog
                {
                    ActionType = "DELETE",
                    TargetType = "AssetCategory",
                    TargetId = "901",
                    Summary = "DELETE /api/categories/901",
                    OccurredAt = DateTime.UtcNow
                },
                new AuditLog
                {
                    ActionType = "DELETE",
                    TargetType = "AssetCategory",
                    TargetId = "902",
                    Summary = "DELETE /api/categories/902/purge",
                    OccurredAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var softDeletes = await _client.GetFromJsonAsync<ApiResult<PagedResult<AuditLogDto>>>(
            "/api/audit-logs?actionType=soft_delete&pageSize=200");
        var purges = await _client.GetFromJsonAsync<ApiResult<PagedResult<AuditLogDto>>>(
            "/api/audit-logs?actionType=purge&pageSize=200");

        softDeletes!.Data!.Items.Should().Contain(x => x.TargetId == "901" && x.ActionType == "soft_delete");
        softDeletes.Data.Items.Should().NotContain(x => x.TargetId == "902");
        purges!.Data!.Items.Should().Contain(x => x.TargetId == "902" && x.ActionType == "purge");
        purges.Data.Items.Should().NotContain(x => x.TargetId == "901");
    }

    private async Task Login()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
    }

    private async Task<T> Post<T>(string url, object data)
    {
        var res = await _client.PostAsJsonAsync(url, data);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object data)
    {
        var res = await _client.PutAsJsonAsync(url, data);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..3].ToUpperInvariant();
}

[ApiController]
[Route("api/test-audit")]
public class AuditProbeController : ControllerBase
{
    [HttpPost("write")]
    public ApiResult<string> Write() => ApiResult<string>.Ok("written");

    [HttpPost("audit-write-failure")]
    public ApiResult<string> AuditWriteFailure([FromServices] AppDbContext db)
    {
        // 留下一条不可持久化的审计实体，模拟业务已完成后审计库写入失败。
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "POST",
            TargetType = "AuditProbe",
            Summary = null!,
            OccurredAt = DateTime.UtcNow
        });
        return ApiResult<string>.Ok("business-succeeded");
    }

    [HttpPost("fail")]
    public ActionResult<ApiResult<object?>> Fail()
        => BadRequest(ApiResult<object?>.Fail(4001, "探针失败"));

    [HttpPost("throw-biz")]
    public ApiResult<object?> ThrowBiz()
        => throw new BizException(4092, "业务校验失败");

    [HttpPost("throw-internal")]
    public ApiResult<object?> ThrowInternal()
        => throw new InvalidOperationException("Server=secret-db;Password=do-not-store");

    [HttpPost("denied")]
    [HasPermission("test:permission-that-does-not-exist")]
    public ApiResult<object?> Denied() => ApiResult<object?>.Ok(null);

    [HttpPut("roles/{id:int}/permissions")]
    public ApiResult<RoleDto> SetRolePermissions(int id)
        => ApiResult<RoleDto>.Ok(new RoleDto
        {
            Id = id,
            Code = "supervisor",
            Name = "部门主管",
            PermissionIds = new[] { 1, 2, 3 },
            MenuIds = new[] { 10, 11 }
        });

    [HttpPut("roles/{id:int}/menus")]
    public ApiResult<RoleDto> SetRoleMenus(int id)
        => ApiResult<RoleDto>.Ok(new RoleDto
        {
            Id = id,
            Code = "supervisor",
            Name = "部门主管",
            PermissionIds = new[] { 1, 2, 3 },
            MenuIds = new[] { 10, 11 }
        });

    [HttpPut("roles/{id:int}/access")]
    public ApiResult<RoleDto> SetRoleAccess(int id)
        => ApiResult<RoleDto>.Ok(new RoleDto
        {
            Id = id,
            Code = "supervisor",
            Name = "部门主管",
            PermissionIds = new[] { 1, 2, 3 },
            MenuIds = new[] { 10, 11 }
        });

    [HttpPost("approvals/{id:int}/approve")]
    public ApiResult<ApprovalFlowDto> Approve(int id)
        => ApiResult<ApprovalFlowDto>.Ok(BuildApprovalFlow(id));

    [HttpPost("approvals/{id:int}/reject")]
    public ApiResult<ApprovalFlowDto> Reject(int id)
        => ApiResult<ApprovalFlowDto>.Ok(BuildApprovalFlow(id) with { Status = "rejected" });

    [HttpPost("approvals/{id:int}/confirm-return")]
    public ApiResult<ApprovalFlowDto> ConfirmReturn(int id)
        => ApiResult<ApprovalFlowDto>.Ok(BuildApprovalFlow(id) with { Status = "returned" });

    [HttpPost("assets/import/confirm")]
    public ApiResult<ImportConfirmResult> AssetImportConfirm()
        => ApiResult<ImportConfirmResult>.Ok(new ImportConfirmResult
        {
            SuccessCount = 2,
            FailedCount = 1,
            Rows = new List<ImportPreviewRow>
            {
                new() { Row = 2, Name = "电脑", CategoryCode = "IT-PC", IsValid = true },
                new() { Row = 3, Name = "显示器", CategoryCode = "IT-MON", IsValid = true },
                new() { Row = 4, Name = "错误资产", CategoryCode = "BAD", IsValid = false, Error = "分类不存在" }
            }
        });

    [HttpPost("users/import")]
    public ApiResult<UserImportResultDto> UserImport()
        => ApiResult<UserImportResultDto>.Ok(new UserImportResultDto
        {
            SuccessCount = 3,
            FailedCount = 1,
            Rows = new List<UserImportRowDto>
            {
                new() { Row = 2, EmployeeNo = "1001", Name = "系统管理员", IsValid = true },
                new() { Row = 3, EmployeeNo = "1002", Name = "张三", IsValid = true },
                new() { Row = 4, EmployeeNo = "1003", Name = "李四", IsValid = true },
                new() { Row = 5, EmployeeNo = "BAD", Name = "错误用户", IsValid = false, Error = "角色不存在" }
            }
        });

    private static ApprovalFlowDto BuildApprovalFlow(int id)
        => new()
        {
            Id = id,
            FlowNo = "AF-TEST-001",
            BizType = "borrow",
            AssetId = 100,
            AssetNo = "A-001",
            AssetName = "测试资产",
            Applicant = "系统管理员",
            Status = "pending"
        };
}
