using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Auth;

public class PermissionPolicyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public PermissionPolicyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(PermissionProbeController).Assembly);
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Protected_permission_endpoint_without_token_returns_401()
    {
        var res = await _client.GetAsync("/api/test-permissions/asset-view");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_permission_endpoint_with_permission_returns_ok()
    {
        var token = await LoginAdmin();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = await _client.GetFromJsonAsync<ApiResult<string>>("/api/test-permissions/asset-view");

        body!.Code.Should().Be(0);
        body.Data.Should().Be("allowed");
    }

    [Fact]
    public async Task Protected_permission_endpoint_without_permission_returns_403()
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = jwt.Create(999, "9001", new[] { "report:view" }, new[] { "employee" });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/test-permissions/asset-view");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> LoginAdmin()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>();
        return body!.Data!.Token;
    }
}

[ApiController]
[Route("api/test-permissions")]
public class PermissionProbeController : ControllerBase
{
    [HttpGet("asset-view")]
    [HasPermission("asset:view")]
    public ApiResult<string> AssetView() => ApiResult<string>.Ok("allowed");
}
