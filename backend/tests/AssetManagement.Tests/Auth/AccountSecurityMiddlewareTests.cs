using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Auth;

public class AccountSecurityMiddlewareTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountSecurityMiddlewareTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Active_account_can_access_business_and_revoked_authorization_takes_effect_immediately()
    {
        var client = _factory.CreateClient();
        var login = await Login(client, "123456");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

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

    [Fact]
    public async Task Default_password_does_not_block_business_and_password_change_revokes_old_token()
    {
        var employeeNo = $"PWD{Guid.NewGuid():N}"[..14];
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reportPermissionId = await db.Permissions
                .Where(x => x.Code == "report:view")
                .Select(x => x.Id)
                .SingleAsync();
            var role = new AssetManagement.Domain.Entities.Role
            {
                Code = $"pwd_{Guid.NewGuid():N}"[..20],
                Name = "初始密码测试角色",
                IsActive = true
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            db.RolePermissions.Add(new AssetManagement.Domain.Entities.RolePermission
            {
                RoleId = role.Id,
                PermissionId = reportPermissionId
            });
            var user = new AssetManagement.Domain.Entities.User
            {
                EmployeeNo = employeeNo,
                Name = "初始密码测试用户",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new AssetManagement.Domain.Entities.UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo,
            password = "123456"
        });
        var login = (await loginResponse.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!.Data!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        (await client.GetAsync("/api/reports/summary")).StatusCode.Should().Be(HttpStatusCode.OK,
            "默认密码账号登录后不应被强制改密流程阻断");

        var changed = await client.PutAsJsonAsync("/api/auth/change-password", new
        {
            oldPassword = "123456",
            newPassword = "Changed12345"
        });
        changed.EnsureSuccessStatusCode();

        (await client.GetAsync("/api/auth/user-info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "修改密码后旧 JWT 必须立即失效");

        var relogin = await Login(client, "Changed12345", employeeNo);
        relogin.Token.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", relogin.Token);
        (await client.GetAsync("/api/reports/summary")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_and_account_reactivation_do_not_leave_old_tokens_usable()
    {
        var employeeNo = $"TOK{Guid.NewGuid():N}"[..14];
        int userId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = new AssetManagement.Domain.Entities.Role
            {
                Code = $"tok_{Guid.NewGuid():N}"[..20],
                Name = "令牌撤销测试角色",
                IsActive = true
            };
            db.Roles.Add(role);
            var user = new AssetManagement.Domain.Entities.User
            {
                EmployeeNo = employeeNo,
                Name = "令牌撤销测试用户",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TokenPass123"),
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new AssetManagement.Domain.Entities.UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var client = _factory.CreateClient();
        var login = await Login(client, "TokenPass123", employeeNo);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        (await client.PostAsync("/api/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/user-info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var secondLogin = await Login(client, "TokenPass123", employeeNo);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondLogin.Token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var rbac = scope.ServiceProvider.GetRequiredService<IRbacService>();
            await rbac.ToggleUserStatusAsync(userId, false);
            await rbac.ToggleUserStatusAsync(userId, true);
        }
        (await client.GetAsync("/api/auth/user-info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<LoginResponse> Login(HttpClient client, string password, string employeeNo = "1001")
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/auth/login", new { employeeNo, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>())!.Data!;
    }
}
