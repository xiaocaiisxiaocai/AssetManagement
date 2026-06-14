using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Auth;

public class VbenContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public VbenContractTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_info_returns_roles_and_permissions_for_admin()
    {
        await Login();

        var body = await _client.GetFromJsonAsync<ApiResult<UserInfoDto>>("/api/auth/user-info");

        body!.Code.Should().Be(0);
        body.Data!.EmployeeNo.Should().Be("1001");
        body.Data.Roles.Should().Contain("admin");
        body.Data.Permissions.Should().Contain("asset:view");
    }

    [Fact]
    public async Task Menu_routes_returns_top_level_routes_for_admin()
    {
        await Login();

        var body = await _client.GetFromJsonAsync<ApiResult<List<RouteDto>>>("/api/menu/routes");

        body!.Code.Should().Be(0);
        var routes = body.Data!;
        var topNames = routes.Select(x => x.Name);
        topNames.Should().Contain("Asset");
        topNames.Should().Contain("Approval");
        topNames.Should().Contain("Report");
        topNames.Should().Contain("Admin");
        routes.Single(x => x.Name == "Asset").Children.Should().Contain(x => x.Name == "AssetList");
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
