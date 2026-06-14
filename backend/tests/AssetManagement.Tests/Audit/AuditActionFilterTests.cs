using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
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
}

[ApiController]
[Route("api/test-audit")]
public class AuditProbeController : ControllerBase
{
    [HttpPost("write")]
    public ApiResult<string> Write() => ApiResult<string>.Ok("written");
}
