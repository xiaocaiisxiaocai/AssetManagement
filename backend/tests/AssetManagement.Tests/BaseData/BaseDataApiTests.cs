using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        var parent = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "研发部"
        });
        var child = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ParentId = parent.Data!.Id,
            Name = "硬件组"
        });

        var tree = await _client.GetFromJsonAsync<ApiResult<List<DepartmentNodeDto>>>("/api/departments/tree");

        var parentNode = tree!.Data!.Single(x => x.Id == parent.Data.Id);
        parentNode.Children.Should().ContainSingle(x => x.Id == child.Data!.Id);
    }

    [Fact]
    public async Task Department_tree_does_not_return_code()
    {
        await Login();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("部门")
        });

        var treeRes = await _client.GetAsync("/api/departments/tree");
        treeRes.EnsureSuccessStatusCode();
        using var treeBody = await JsonDocument.ParseAsync(await treeRes.Content.ReadAsStreamAsync());

        var node = treeBody.RootElement.GetProperty("data").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == department.Data!.Id);
        node.TryGetProperty("code", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Category_update_recalculates_descendant_codes()
    {
        await Login();
        var seg = Unique("PLC");
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = seg
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = "MIT"
        });

        await Put<ApiResult<CategoryNodeDto>>($"/api/categories/{root.Data.Id}", new UpdateCategoryRequest
        {
            CodeSeg = $"{seg}X"
        });
        var tree = await _client.GetFromJsonAsync<ApiResult<List<CategoryNodeDto>>>("/api/categories/tree");

        var updatedChild = tree!.Data!
            .Single(x => x.Id == root.Data.Id)
            .Children.Single(x => x.Id == child.Data!.Id);
        updatedChild.Code.Should().Be($"{seg}X-MIT");
    }

    [Fact]
    public async Task Category_tree_uses_code_and_optional_child_remark_without_name()
    {
        await Login();
        var seg = Unique("CAT");
        var rootRes = await _client.PostAsJsonAsync("/api/categories", new
        {
            codeSeg = seg,
            remark = "一级备注应忽略"
        });
        rootRes.EnsureSuccessStatusCode();
        using var rootBody = await JsonDocument.ParseAsync(await rootRes.Content.ReadAsStreamAsync());
        var rootId = rootBody.RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var childRes = await _client.PostAsJsonAsync("/api/categories", new
        {
            parentId = rootId,
            codeSeg = "M1",
            remark = "二级备注"
        });
        childRes.EnsureSuccessStatusCode();

        var treeRes = await _client.GetAsync("/api/categories/tree");
        treeRes.EnsureSuccessStatusCode();
        using var treeBody = await JsonDocument.ParseAsync(await treeRes.Content.ReadAsStreamAsync());

        var root = treeBody.RootElement.GetProperty("data").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == rootId);
        root.TryGetProperty("name", out _).Should().BeFalse();
        root.GetProperty("remark").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("code").GetString().Should().Be(seg);

        var child = root.GetProperty("children").EnumerateArray().Single();
        child.TryGetProperty("name", out _).Should().BeFalse();
        child.GetProperty("remark").GetString().Should().Be("二级备注");
        child.GetProperty("code").GetString().Should().Be($"{seg}-M1");
    }

    [Fact]
    public async Task Category_create_rejects_fourth_level()
    {
        await Login();
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("L1")
        });
        var second = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = "L2"
        });
        var third = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = second.Data!.Id,
            CodeSeg = "L3"
        });

        var res = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            ParentId = third.Data!.Id,
            CodeSeg = "L4"
        });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
        body!.Code.Should().NotBe(0);
        body.Message.Should().Contain("最多维护三级");
    }

    [Fact]
    public async Task Category_update_rejects_move_that_exceeds_third_level()
    {
        await Login();
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("R")
        });
        var second = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = "S"
        });
        await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = second.Data!.Id,
            CodeSeg = "T"
        });
        var anotherRoot = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Unique("A")
        });
        var anotherSecond = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = anotherRoot.Data!.Id,
            CodeSeg = "B"
        });

        var res = await _client.PutAsJsonAsync($"/api/categories/{second.Data!.Id}", new UpdateCategoryRequest
        {
            ParentId = anotherSecond.Data!.Id,
            CodeSeg = second.Data.CodeSeg
        });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
        body!.Code.Should().NotBe(0);
        body.Message.Should().Contain("最多维护三级");
    }

    [Fact]
    public async Task Location_tree_returns_flat_locations()
    {
        await Login();
        var rootName = Unique("仓库");
        var root = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = rootName
        });
        var area = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = "A区"
        });
        var shelf = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = "A-01"
        });

        var tree = await _client.GetFromJsonAsync<ApiResult<List<LocationNodeDto>>>("/api/locations/tree");

        tree!.Data!.Should().Contain(x => x.Id == root.Data!.Id);
        tree.Data.Should().Contain(x => x.Id == area.Data!.Id);
        tree.Data.Should().Contain(x => x.Id == shelf.Data!.Id);
    }

    [Fact]
    public async Task Location_tree_does_not_return_qr_code()
    {
        await Login();
        var root = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = Unique("仓库")
        });

        var treeRes = await _client.GetAsync("/api/locations/tree");
        treeRes.EnsureSuccessStatusCode();
        using var treeBody = await JsonDocument.ParseAsync(await treeRes.Content.ReadAsStreamAsync());

        var rootNode = treeBody.RootElement.GetProperty("data").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == root.Data!.Id);
        rootNode.TryGetProperty("qrCode", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Location_tree_is_flat_without_parent_fields()
    {
        await Login();
        var root = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = Unique("库位")
        });

        var treeRes = await _client.GetAsync("/api/locations/tree");
        treeRes.EnsureSuccessStatusCode();
        using var treeBody = await JsonDocument.ParseAsync(await treeRes.Content.ReadAsStreamAsync());

        var rootNode = treeBody.RootElement.GetProperty("data").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == root.Data!.Id);
        rootNode.TryGetProperty("parentId", out _).Should().BeFalse();
        rootNode.TryGetProperty("children", out _).Should().BeFalse();
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
