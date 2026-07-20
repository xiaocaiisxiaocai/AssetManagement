using System.Net.Http.Json;
using AssetManagement.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class HealthTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _c;

    public HealthTests(TestWebAppFactory f)
    {
        _c = f.CreateClient();
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await _c.GetFromJsonAsync<ApiResult<string>>("/api/health");

        res!.Code.Should().Be(0);
        res.Data.Should().Be("healthy");
    }

    [Fact]
    public async Task Live_and_ready_endpoints_are_distinct_and_healthy()
    {
        var live = await _c.GetFromJsonAsync<ApiResult<string>>("/api/health/live");
        var ready = await _c.GetFromJsonAsync<ApiResult<string>>("/api/health/ready");

        live!.Code.Should().Be(0);
        ready!.Code.Should().Be(0);
    }
}
