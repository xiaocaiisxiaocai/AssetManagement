using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.BaseData;

public class BaseDataApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public BaseDataApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Department_tree_returns_nested_children()
    {
        await Login();
        var code = Unique("D");
        var parent = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "研发部",
            Code = code
        });
        var child = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ParentId = parent.Data!.Id,
            Name = "硬件组",
            Code = $"{code}-HW"
        });

        var tree = await _client.GetFromJsonAsync<ApiResult<List<DepartmentNodeDto>>>("/api/departments/tree");

        var parentNode = tree!.Data!.Single(x => x.Id == parent.Data.Id);
        parentNode.Children.Should().ContainSingle(x => x.Id == child.Data!.Id);
    }

    [Fact]
    public async Task Category_update_recalculates_descendant_codes()
    {
        await Login();
        var seg = Unique("PLC");
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            Name = "PLC",
            CodeSeg = seg
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            Name = "模块",
            CodeSeg = "MIT"
        });

        await Put<ApiResult<CategoryNodeDto>>($"/api/categories/{root.Data.Id}", new UpdateCategoryRequest
        {
            Name = "PLC升级",
            CodeSeg = $"{seg}X"
        });
        var tree = await _client.GetFromJsonAsync<ApiResult<List<CategoryNodeDto>>>("/api/categories/tree");

        var updatedChild = tree!.Data!
            .Single(x => x.Id == root.Data.Id)
            .Children.Single(x => x.Id == child.Data!.Id);
        updatedChild.Code.Should().Be($"{seg}X-MIT");
    }

    [Fact]
    public async Task Location_tree_returns_three_levels()
    {
        await Login();
        var rootName = Unique("仓库");
        var root = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = rootName
        });
        var area = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            ParentId = root.Data!.Id,
            Name = "A区"
        });
        var shelf = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            ParentId = area.Data!.Id,
            Name = "A-01"
        });

        var tree = await _client.GetFromJsonAsync<ApiResult<List<LocationNodeDto>>>("/api/locations/tree");

        var rootNode = tree!.Data!.Single(x => x.Id == root.Data.Id);
        rootNode.Children.Single().Children.Should().ContainSingle(x => x.Id == shelf.Data!.Id);
    }

    [Fact]
    public async Task Settings_save_then_read_returns_updated_value()
    {
        await Login();
        var key = Unique("setting");

        await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = key,
                Value = "42",
                Description = "测试参数"
            }
        });
        var settings = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");

        settings!.Data!.Should().Contain(x => x.Key == key && x.Value == "42");
    }

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];
}
