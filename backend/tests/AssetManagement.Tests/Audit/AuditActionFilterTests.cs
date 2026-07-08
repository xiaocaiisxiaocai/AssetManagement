using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
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
            CategoryId = category.Data!.Id,
            Brand = "旧品牌"
        });

        var updated = await Put<ApiResult<AssetDto>>($"/api/assets/{created.Data!.Id}", new UpdateAssetRequest
        {
            Name = "审计后名称",
            CategoryId = category.Data.Id,
            Brand = "新品牌",
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

    [HttpPost("fail")]
    public ActionResult<ApiResult<object?>> Fail()
        => BadRequest(ApiResult<object?>.Fail(4001, "探针失败"));
}
