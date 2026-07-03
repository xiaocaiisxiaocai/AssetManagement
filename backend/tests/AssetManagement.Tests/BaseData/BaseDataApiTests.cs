using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Assets;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.TestMaterials;
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
    public async Task Department_create_allows_empty_manager()
    {
        await Login();

        var created = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("部门")
        });

        created.Code.Should().Be(0);
        created.Data!.ManagerId.Should().BeNull();
        created.Data.ManagerName.Should().BeNull();
    }

    [Fact]
    public async Task Department_tree_returns_nested_children()
    {
        await Login();
        var parentManager = await CreateUser();
        var childManager = await CreateUser();
        var parent = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = parentManager.Id,
            Name = "研发部"
        });
        var child = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = childManager.Id,
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
        var manager = await CreateUser();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
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
    public async Task Department_rejects_duplicate_name_with_business_message()
    {
        await Login();
        var name = Unique("部门重名");
        var existingManager = await CreateUser();
        var targetManager = await CreateUser();
        var createManager = await CreateUser();
        var updateManager = await CreateUser();
        await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = existingManager.Id,
            Name = name
        });
        var target = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = targetManager.Id,
            Name = Unique("待改名部门")
        });

        var duplicatedCreate = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = createManager.Id,
            Name = name
        });
        var duplicatedUpdate = await Put<ApiResult<DepartmentNodeDto>>(
            $"/api/departments/{target.Data!.Id}",
            new UpdateDepartmentRequest
            {
                ManagerId = updateManager.Id,
                Name = name,
                IsActive = true
            });

        duplicatedCreate.Code.Should().Be(4094);
        duplicatedCreate.Message.Should().Be("部门名称已存在");
        duplicatedUpdate.Code.Should().Be(4094);
        duplicatedUpdate.Message.Should().Be("部门名称已存在");
    }

    [Fact]
    public async Task Department_rejects_duplicate_manager_on_active_departments()
    {
        await Login();
        var manager = await CreateUser();
        var targetManager = await CreateUser();
        await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("负责人部门")
        });
        var target = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = targetManager.Id,
            Name = Unique("待换负责人")
        });

        var duplicatedCreate = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("重复负责人")
        });
        var duplicatedUpdate = await Put<ApiResult<DepartmentNodeDto>>(
            $"/api/departments/{target.Data!.Id}",
            new UpdateDepartmentRequest
            {
                ManagerId = manager.Id,
                Name = target.Data.Name,
                IsActive = true
            });

        duplicatedCreate.Code.Should().Be(4094);
        duplicatedCreate.Message.Should().Be("负责人已负责其他部门");
        duplicatedUpdate.Code.Should().Be(4094);
        duplicatedUpdate.Message.Should().Be("负责人已负责其他部门");
    }

    [Fact]
    public async Task Department_inactive_department_does_not_occupy_manager()
    {
        await Login();
        var manager = await CreateUser();
        var inactive = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("停用负责人")
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{inactive.Data!.Id}", new UpdateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = inactive.Data.Name,
            IsActive = false
        });

        var created = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("复用负责人")
        });

        created.Code.Should().Be(0);
    }

    [Fact]
    public async Task Department_inactive_update_can_use_occupied_manager()
    {
        await Login();
        var occupiedManager = await CreateUser();
        var targetManager = await CreateUser();
        await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = occupiedManager.Id,
            Name = Unique("已占负责人")
        });
        var target = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = targetManager.Id,
            Name = Unique("停用复用")
        });

        var updated = await Put<ApiResult<DepartmentNodeDto>>(
            $"/api/departments/{target.Data!.Id}",
            new UpdateDepartmentRequest
            {
                ManagerId = occupiedManager.Id,
                Name = target.Data.Name,
                IsActive = false
            });

        updated.Code.Should().Be(0);
        updated.Data!.ManagerId.Should().Be(occupiedManager.Id);
        updated.Data.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Category_update_recalculates_descendant_codes()
    {
        await Login();
        var seg = UniqueCodeSeg();
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = seg
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
        });
        var childData = child.Data!;
        var nextSeg = UniqueCodeSeg();

        await Put<ApiResult<CategoryNodeDto>>($"/api/categories/{root.Data.Id}", new UpdateCategoryRequest
        {
            CodeSeg = nextSeg
        });
        var tree = await _client.GetFromJsonAsync<ApiResult<List<CategoryNodeDto>>>("/api/categories/tree");

        var updatedChild = tree!.Data!
            .Single(x => x.Id == root.Data.Id)
            .Children.Single(x => x.Id == childData.Id);
        updatedChild.Code.Should().Be($"{nextSeg}-{childData.CodeSeg}");
    }

    [Fact]
    public async Task Category_create_validates_level_length_and_regex()
    {
        await Login();
        try
        {
            await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
            {
                new SaveSystemSettingRequest { Key = "category_code_level1_length", Value = "2-4" },
                new SaveSystemSettingRequest { Key = "category_code_level1_regex", Value = "^[A-Za-z]{2,4}$" },
                new SaveSystemSettingRequest { Key = "category_code_level2_length", Value = "3-5" },
                new SaveSystemSettingRequest { Key = "category_code_level2_regex", Value = "^[0-9]{3,5}$" },
                new SaveSystemSettingRequest { Key = "category_code_level3_length", Value = "2-6" },
                new SaveSystemSettingRequest { Key = "category_code_level3_regex", Value = "^[A-Za-z0-9]{2,6}$" },
            });
            var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
            {
                CodeSeg = "AB"
            });

            var invalidLength = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
            {
                ParentId = root.Data!.Id,
                CodeSeg = "12"
            });
            var invalidRegex = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
            {
                ParentId = root.Data.Id,
                CodeSeg = "ABC"
            });
            var valid = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
            {
                ParentId = root.Data.Id,
                CodeSeg = "12345"
            });

            invalidLength.Code.Should().Be(4001);
            invalidLength.Message.Should().Contain("二级分类编码段必须是 3-5 位");
            invalidRegex.Code.Should().Be(4001);
            invalidRegex.Message.Should().Contain("二级分类编码段格式不正确");
            valid.Code.Should().Be(0);
            valid.Data!.Code.Should().Be("AB-12345");
        }
        finally
        {
            await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
            {
                new SaveSystemSettingRequest { Key = "category_code_level1_length", Value = "2-6" },
                new SaveSystemSettingRequest { Key = "category_code_level1_regex", Value = "^[A-Za-z0-9]+$" },
                new SaveSystemSettingRequest { Key = "category_code_level2_length", Value = "2-6" },
                new SaveSystemSettingRequest { Key = "category_code_level2_regex", Value = "^[A-Za-z0-9]+$" },
                new SaveSystemSettingRequest { Key = "category_code_level3_length", Value = "2-6" },
                new SaveSystemSettingRequest { Key = "category_code_level3_regex", Value = "^[A-Za-z0-9]+$" },
            });
        }
    }

    [Fact]
    public async Task Category_tree_uses_code_and_optional_child_remark_without_name()
    {
        await Login();
        var seg = UniqueCodeSeg();
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
            CodeSeg = UniqueCodeSeg()
        });
        var second = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
        });
        var third = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = second.Data!.Id,
            CodeSeg = UniqueThirdLevelCodeSeg()
        });

        var res = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            ParentId = third.Data!.Id,
            CodeSeg = UniqueThirdLevelCodeSeg()
        });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
        body!.Code.Should().NotBe(0);
        body.Message.Should().Contain("最多维护三级");
    }

    [Fact]
    public async Task Category_create_rejects_duplicate_code_with_business_message()
    {
        await Login();
        var codeSeg = UniqueCodeSeg();
        await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = codeSeg
        });

        var res = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = codeSeg
        });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
        body!.Code.Should().Be(4094);
        body.Message.Should().Contain("已存在对应编码段");
    }

    [Fact]
    public async Task Category_update_rejects_move_that_exceeds_third_level()
    {
        await Login();
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });
        var second = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
        });
        await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = second.Data!.Id,
            CodeSeg = UniqueThirdLevelCodeSeg()
        });
        var anotherRoot = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });
        var anotherSecond = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = anotherRoot.Data!.Id,
            CodeSeg = UniqueCodeSeg()
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
    public async Task Location_create_rejects_duplicate_name_with_business_message()
    {
        await Login();
        var name = Unique("库位");
        await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = name
        });

        var duplicated = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = name
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("存放位置已存在");
    }

    [Fact]
    public async Task Location_update_rejects_duplicate_name_with_business_message()
    {
        await Login();
        var existing = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = Unique("库位")
        });
        var target = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = Unique("库位")
        });

        var duplicated = await Put<ApiResult<LocationNodeDto>>($"/api/locations/{target.Data!.Id}", new UpdateLocationRequest
        {
            Name = existing.Data!.Name
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("存放位置已存在");
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
    public async Task Department_with_asset_or_material_references_cannot_be_deleted()
    {
        await Login();
        var manager = await CreateUser();
        var assetDepartment = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("资产占用部门")
        });
        var category = await CreateCategory();
        await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "占用部门资产",
            CategoryId = category.Id,
            DepartmentId = assetDepartment.Data!.Id
        });

        var assetDepartmentDeleted = await Delete<ApiResult<object?>>($"/api/departments/{assetDepartment.Data.Id}");

        assetDepartmentDeleted.Code.Should().Be(4094);
        assetDepartmentDeleted.Message.Should().Contain("部门已被资产使用");

        var materialDepartment = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = (await CreateUser()).Id,
            Name = Unique("料件占用部门")
        });
        var project = await CreateProject();
        await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "占用部门料件",
            ProjectId = project.Id,
            DepartmentId = materialDepartment.Data!.Id
        });

        var materialDepartmentDeleted = await Delete<ApiResult<object?>>($"/api/departments/{materialDepartment.Data.Id}");

        materialDepartmentDeleted.Code.Should().Be(4094);
        materialDepartmentDeleted.Message.Should().Contain("部门已被测试料件使用");
    }

    [Fact]
    public async Task Location_with_asset_or_material_references_cannot_be_deleted()
    {
        await Login();
        var assetLocation = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = Unique("资产占用位置")
        });
        var category = await CreateCategory();
        await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "占用位置资产",
            CategoryId = category.Id,
            LocationId = assetLocation.Data!.Id
        });

        var assetLocationDeleted = await Delete<ApiResult<object?>>($"/api/locations/{assetLocation.Data.Id}");

        assetLocationDeleted.Code.Should().Be(4094);
        assetLocationDeleted.Message.Should().Contain("位置已被资产使用");

        var materialLocation = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = Unique("料件占用位置")
        });
        var project = await CreateProject();
        await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "占用位置料件",
            ProjectId = project.Id,
            LocationId = materialLocation.Data!.Id
        });

        var materialLocationDeleted = await Delete<ApiResult<object?>>($"/api/locations/{materialLocation.Data.Id}");

        materialLocationDeleted.Code.Should().Be(4094);
        materialLocationDeleted.Message.Should().Contain("位置已被测试料件使用");
    }

    [Fact]
    public async Task Settings_save_then_read_returns_updated_value()
    {
        await Login();

        await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "page_size",
                Value = "42",
                Description = "默认每页记录数"
            }
        });
        var settings = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");

        settings!.Data!.Should().Contain(x => x.Key == "page_size" && x.Value == "42");
    }

    [Fact]
    public async Task Settings_save_rejects_unknown_key_and_keeps_existing_settings()
    {
        await Login();
        var before = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");
        var unknownKey = Unique("setting");

        var response = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = unknownKey,
                Value = "42",
                Description = "不应新增"
            }
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<List<SystemSettingDto>>>();
        var after = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");

        body!.Code.Should().Be(4001);
        after!.Data!.Select(x => x.Key).Should().BeEquivalentTo(before!.Data!.Select(x => x.Key));
    }

    [Theory]
    [InlineData("audit_cleanup_enabled", "yes", "布尔值")]
    [InlineData("database_backup_time", "25:61", "时间")]
    [InlineData("attachment_max_mb", "101", "1-100")]
    [InlineData("page_size", "201", "1-200")]
    [InlineData("category_code_level1_length", "21", "1-20")]
    [InlineData("category_code_level1_length", "8-2", "长度范围")]
    [InlineData("category_code_level1_length", "abc", "1-20")]
    [InlineData("category_code_level1_regex", "[", "合法正则表达式")]
    [InlineData("audit_retention_days", "15", "7/14/30")]
    [InlineData("database_backup_path", " ", "不能为空")]
    public async Task Settings_save_rejects_invalid_value(string key, string value, string message)
    {
        await Login();

        var response = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = key,
                Value = value,
                Description = "非法值不应保存"
            }
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<List<SystemSettingDto>>>();

        body!.Code.Should().Be(4001);
        body.Message.Should().Contain(message);
    }

    [Fact]
    public async Task Settings_save_updates_value_without_changing_description()
    {
        await Login();
        var before = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");
        var originalDescription = before!.Data!.Single(x => x.Key == "page_size").Description;

        await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "page_size",
                Value = "43",
                Description = "前端不允许编辑说明，后端也应忽略"
            }
        });
        var after = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");

        after!.Data!.Should().Contain(x =>
            x.Key == "page_size"
            && x.Value == "43"
            && x.Description == originalDescription);
    }

    [Fact]
    public async Task Runtime_settings_exposes_page_size_for_normal_pages()
    {
        await Login();
        await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "page_size",
                Value = "50",
                Description = "默认每页记录数"
            }
        });

        var runtime = await _client.GetFromJsonAsync<ApiResult<RuntimeSettingsDto>>("/api/settings/runtime");

        runtime!.Data!.PageSize.Should().Be(50);
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

    private async Task<T> Delete<T>(string url)
    {
        var res = await _client.DeleteAsync(url);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<UserDto> CreateUser()
    {
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("u"),
            Name = "部门负责人",
            RoleIds = new[] { await CreateRoleId() }
        });
        return user.Data!;
    }

    private async Task<int> CreateRoleId()
    {
        var role = await Post<ApiResult<RoleDto>>("/api/roles", new RoleDto
        {
            Code = Unique("role"),
            Name = Unique("测试角色"),
            IsActive = true
        });
        return role.Data!.Id;
    }

    private async Task<CategoryNodeDto> CreateCategory()
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
        });
        return child.Data!;
    }

    private async Task<TestProjectDto> CreateProject()
        => (await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = Unique("TP"),
            Name = Unique("测试项目")
        })).Data!;

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..2].ToUpperInvariant();

    private static string UniqueThirdLevelCodeSeg()
        => Guid.NewGuid().ToString("N")[..3].ToUpperInvariant();
}
