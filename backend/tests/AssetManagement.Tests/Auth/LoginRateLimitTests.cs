using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AssetManagement.Tests.Auth;

public class LoginRateLimitTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LoginRateLimitTests(TestWebAppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:LoginRateLimitEnabled"] = "true"
                })));
    }

    [Fact]
    public async Task Login_endpoint_returns_429_after_fixed_window_limit()
    {
        var client = _factory.CreateClient();
        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                employeeNo = $"missing-{i}",
                password = "invalid"
            });
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo = "missing-final",
            password = "invalid"
        });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
