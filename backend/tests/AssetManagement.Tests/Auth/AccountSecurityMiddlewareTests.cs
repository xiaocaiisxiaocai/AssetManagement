using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Auth;

public class AccountSecurityMiddlewareTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountSecurityMiddlewareTests(TestWebAppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:EnforcePasswordChange"] = "true"
                })));
    }

    [Fact]
    public async Task Default_password_is_restricted_and_revoked_authorization_takes_effect_immediately()
    {
        var client = _factory.CreateClient();
        var login = await Login(client, "123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var restricted = await client.GetAsync("/api/reports/summary");
        restricted.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await restricted.Content.ReadFromJsonAsync<ApiResult<object?>>())!.Code.Should().Be(4031);

        var changed = await client.PutAsJsonAsync("/api/auth/change-password", new
        {
            oldPassword = "123456",
            newPassword = "Secure2026"
        });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshedLogin = await Login(client, "Secure2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedLogin.Token);
        (await client.GetAsync("/api/reports/summary")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reportPermissionId = await db.Permissions
                .Where(x => x.Code == "report:view")
                .Select(x => x.Id)
                .SingleAsync();
            await db.RolePermissions.Where(x => x.PermissionId == reportPermissionId).ExecuteDeleteAsync();
        }

        (await client.GetAsync("/api/reports/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "旧 JWT 中的权限不得在数据库撤权后继续生效");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(x => x.EmployeeNo == "1001")
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false));
        }

        (await client.GetAsync("/api/auth/user-info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "账号禁用后旧 JWT 必须立即失效");
    }

    private static async Task<LoginResponse> Login(HttpClient client, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/auth/login", new { employeeNo = "1001", password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!.Data!;
    }
}
