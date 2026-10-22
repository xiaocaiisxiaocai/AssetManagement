using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Assets;

public class AssetOverviewApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public AssetOverviewApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Overview_requires_asset_view_permission()
    {
        var token = await CreateTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/assets/overview");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_with_only_asset_view_permission_returns_zero_counts_for_empty_asset_table()
    {
        var token = await CreateTokenAsync("asset:view");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/assets/overview");
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AssetOverviewDto>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "资产总览只要求 asset:view，不应依赖 report:view 或 category:view");
        body!.Code.Should().Be(0);
        body.Data!.Total.Should().Be(0);
        body.Data.Available.Should().Be(0);
        body.Data.Borrowed.Should().Be(0);
        body.Data.Maintenance.Should().Be(0);
        body.Data.Scrapped.Should().Be(0);
        body.Data.UncategorizedTotal.Should().Be(0);
        body.Data.Categories.Should().OnlyContain(category =>
            category.Total == 0
            && category.Available == 0
            && category.Borrowed == 0
            && category.Maintenance == 0
            && category.Scrapped == 0);
    }

    private async Task<string> CreateTokenAsync(params string[] permissionCodes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new Role
        {
            Code = $"overview-{Guid.NewGuid():N}"[..20],
            Name = $"总览权限测试{Guid.NewGuid():N}"[..24],
            IsActive = true
        };
        var user = new User
        {
            EmployeeNo = $"OV{Guid.NewGuid():N}"[..12],
            Name = "资产总览受限用户",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("not-used-123"),
            IsActive = true
        };
        db.AddRange(role, user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        foreach (var permissionCode in permissionCodes)
        {
            var permissionId = await db.Permissions
                .Where(permission => permission.Code == permissionCode)
                .Select(permission => permission.Id)
                .SingleAsync();
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId
            });
        }
        await db.SaveChangesAsync();

        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        return jwt.Create(user.Id, user.EmployeeNo, permissionCodes, [role.Code]);
    }
}
