using System.Net;
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
    public async Task Organization_levels_are_exposed_and_department_level_is_persisted()
    {
        await Login();

        var levels = await _client.GetFromJsonAsync<ApiResult<List<OrganizationLevelDto>>>(
            "/api/departments/levels");
        var created = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("课别"),
            OrganizationLevelCode = "section"
        });

        levels!.Data!.Select(x => x.Code)
            .Should().Contain(new[] { "company", "division", "department", "section" });
        created.Data!.OrganizationLevelCode.Should().Be("section");
        created.Data.OrganizationLevelName.Should().Be("课别");
    }

    [Fact]
    public async Task Organization_hierarchy_allows_division_to_skip_to_section_and_rejects_children_under_section()
    {
        await Login();
        var company = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("公司"),
            OrganizationLevelCode = "company"
        });
        var division = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("事业部"),
            ParentId = company.Data!.Id,
            OrganizationLevelCode = "division"
        });
        var section = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("直属课别"),
            ParentId = division.Data!.Id,
            OrganizationLevelCode = "section"
        });

        var invalidResponse = await _client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("非法下级"),
            ParentId = section.Data!.Id,
            OrganizationLevelCode = "department"
        });
        var invalid = await invalidResponse.Content.ReadFromJsonAsync<ApiResult<DepartmentNodeDto>>();

        section.Data!.OrganizationLevelCode.Should().Be("section");
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalid!.Code.Should().Be(4001);
        invalid.Message.Should().Be("课别不能新增下级组织");
    }

    [Fact]
    public async Task Organization_hierarchy_defaults_follow_company_division_department_section_order()
    {
        await Login();
        var company = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("默认公司")
        });
        var division = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("默认事业部"),
            ParentId = company.Data!.Id
        });
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("默认部门"),
            ParentId = division.Data!.Id
        });
        var section = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("默认课别"),
            ParentId = department.Data!.Id
        });

        company.Data.OrganizationLevelCode.Should().Be("company");
        division.Data.OrganizationLevelCode.Should().Be("division");
        department.Data.OrganizationLevelCode.Should().Be("department");
        section.Data!.OrganizationLevelCode.Should().Be("section");
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
    public async Task Department_cannot_be_moved_under_its_descendant()
    {
        await Login();
        var parent = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("循环父部门")
        });
        var child = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ParentId = parent.Data!.Id,
            Name = Unique("循环子部门")
        });

        var response = await _client.PutAsJsonAsync(
            $"/api/departments/{parent.Data.Id}",
            new UpdateDepartmentRequest
            {
                ParentId = child.Data!.Id,
                Name = parent.Data.Name,
                IsActive = true
            });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<DepartmentNodeDto>>();

        result!.Code.Should().Be(4001);
        result.Message.Should().Contain("子部门");
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
    public async Task Department_options_allows_employee_without_department_view_and_exposes_manager_selection_fields()
    {
        await Login();
        var manager = await CreateUser();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("选项部门")
        });
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?page=1&pageSize=100");
        var employeeRole = roles!.Data!.Items.Single(x => x.Code == "employee");
        var employeeNo = $"emp{Guid.NewGuid():N}"[..16];
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = "员工选项测试",
            Password = "TestPass123",
            RoleIds = new[] { employeeRole.Id }
        });
        var employeeLogin = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "TestPass123"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", employeeLogin.Data!.Token);

        var response = await _client.GetAsync("/api/departments/options");

        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var option = body.RootElement.GetProperty("data").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == department.Data!.Id);
        option.EnumerateObject().Select(x => x.Name)
            .Should().BeEquivalentTo("id", "name", "managerId", "managerName", "isActive", "children");
        option.GetProperty("managerId").GetInt32().Should().Be(manager.Id);
        option.GetProperty("managerName").GetString().Should().Be(manager.Name);
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

        var duplicatedCreateResponse = await _client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = createManager.Id,
            Name = name
        });
        var duplicatedUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/departments/{target.Data!.Id}",
            new UpdateDepartmentRequest
            {
                ManagerId = updateManager.Id,
                Name = name,
                IsActive = true
            });
        duplicatedCreateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicatedUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicatedCreate = await duplicatedCreateResponse.Content.ReadFromJsonAsync<ApiResult<DepartmentNodeDto>>();
        var duplicatedUpdate = await duplicatedUpdateResponse.Content.ReadFromJsonAsync<ApiResult<DepartmentNodeDto>>();

        duplicatedCreate!.Code.Should().Be(4094);
        duplicatedCreate.Message.Should().Be("部门名称已存在");
        duplicatedUpdate!.Code.Should().Be(4094);
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

        var duplicatedCreateResponse = await _client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = Unique("重复负责人")
        });
        var duplicatedUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/departments/{target.Data!.Id}",
            new UpdateDepartmentRequest
            {
                ManagerId = manager.Id,
                Name = target.Data.Name,
                IsActive = true
            });
        duplicatedCreateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicatedUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicatedCreate = await duplicatedCreateResponse.Content.ReadFromJsonAsync<ApiResult<DepartmentNodeDto>>();
        var duplicatedUpdate = await duplicatedUpdateResponse.Content.ReadFromJsonAsync<ApiResult<DepartmentNodeDto>>();

        duplicatedCreate!.Code.Should().Be(4094);
        duplicatedCreate.Message.Should().Be("负责人已负责其他部门");
        duplicatedUpdate!.Code.Should().Be(4094);
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

            var invalidLengthResponse = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
            {
                ParentId = root.Data!.Id,
                CodeSeg = "12"
            });
            var invalidRegexResponse = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
            {
                ParentId = root.Data.Id,
                CodeSeg = "ABC"
            });
            invalidLengthResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            invalidRegexResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var invalidLength = await invalidLengthResponse.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
            var invalidRegex = await invalidRegexResponse.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
            var valid = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
            {
                ParentId = root.Data.Id,
                CodeSeg = "12345"
            });

            invalidLength!.Code.Should().Be(4001);
            invalidLength.Message.Should().Contain("二级分类编码段必须是 3-5 位");
            invalidRegex!.Code.Should().Be(4001);
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
    public async Task Category_create_rejects_child_remark_longer_than_database_limit()
    {
        await Login();
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });

        var response = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg(),
            Remark = new string('备', 501)
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();

        result!.Code.Should().Be(4001);
        result.Message.Should().Be("The field Remark must be a string or array type with a maximum length of '500'.");
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

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
        body!.Code.Should().Be(4096);
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

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();
        body!.Code.Should().Be(4096);
        body.Message.Should().Contain("最多维护三级");
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

        var assetDepartmentDeleteResponse = await _client.DeleteAsync($"/api/departments/{assetDepartment.Data.Id}");
        assetDepartmentDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var assetDepartmentDeleted = await assetDepartmentDeleteResponse.Content.ReadFromJsonAsync<ApiResult<object?>>();

        assetDepartmentDeleted!.Code.Should().Be(4094);
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

        var materialDepartmentDeleteResponse = await _client.DeleteAsync($"/api/departments/{materialDepartment.Data.Id}");
        materialDepartmentDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var materialDepartmentDeleted = await materialDepartmentDeleteResponse.Content.ReadFromJsonAsync<ApiResult<object?>>();

        materialDepartmentDeleted!.Code.Should().Be(4094);
        materialDepartmentDeleted.Message.Should().Contain("部门已被测试料件使用");
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<List<SystemSettingDto>>>();
        var after = await _client.GetFromJsonAsync<ApiResult<List<SystemSettingDto>>>("/api/settings");

        body!.Code.Should().Be(4001);
        body.Message.Should().Contain("不存在");
        after!.Data!.Select(x => x.Key).Should().BeEquivalentTo(before!.Data!.Select(x => x.Key));
    }

    [Theory]
    [InlineData("audit_cleanup_enabled", "yes", "布尔值")]
    [InlineData("database_backup_time", "25:61", "时间")]
    [InlineData("attachment_max_mb", "101", "1-100")]
    [InlineData("asset_condition_options", "[\"正常\",\"正常\"]", "不重复")]
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<List<SystemSettingDto>>>();

        body!.Code.Should().Be(4001);
        body.Message.Should().Contain(message);
    }

    [Fact]
    public async Task Settings_save_rejects_normalized_value_longer_than_database_limit()
    {
        await Login();

        var response = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "database_backup_path",
                Value = new string('路', 501)
            }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<List<SystemSettingDto>>>();

        var longOptions = Enumerable.Range(1, 20)
            .Select(index => $"{index:D2}{new string('状', 24)}")
            .ToList();
        var dictionaryResponse = await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "asset_condition_options",
                Value = JsonSerializer.Serialize(longOptions)
            }
        });
        dictionaryResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var dictionaryBody = await dictionaryResponse.Content
            .ReadFromJsonAsync<ApiResult<List<SystemSettingDto>>>();

        body!.Code.Should().Be(4001);
        body.Message.Should().Contain("500");
        dictionaryBody!.Code.Should().Be(4001);
        dictionaryBody.Message.Should().Contain("500");
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

    [Fact]
    public async Task Asset_condition_dictionary_can_be_configured_and_exposed_to_business_forms()
    {
        await Login();
        const string defaults = "[\"正常使用\",\"轻微损坏\",\"待维修\",\"维修中\",\"停用\"]";

        try
        {
            await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
            {
                new SaveSystemSettingRequest
                {
                    Key = "asset_condition_options",
                    Value = "[\"完好\",\"待检修\"]"
                }
            });

            var runtime = await _client.GetFromJsonAsync<ApiResult<RuntimeSettingsDto>>("/api/settings/runtime");

            runtime!.Data!.AssetConditionOptions.Should().Equal("完好", "待检修");
        }
        finally
        {
            await Put<ApiResult<List<SystemSettingDto>>>("/api/settings", new[]
            {
                new SaveSystemSettingRequest
                {
                    Key = "asset_condition_options",
                    Value = defaults
                }
            });
        }
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

    private async Task<UserDto> CreateUser()
    {
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles?pageSize=100");
        var supervisorRole = roles!.Data!.Items.Single(x => x.Code == "supervisor");
        var seededSupervisor = await _client.GetFromJsonAsync<ApiResult<PagedResult<UserDto>>>(
            "/api/users?keyword=TEST-SUPERVISOR");
        var user = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("u"),
            Name = "部门负责人",
            DepartmentId = seededSupervisor!.Data!.Items.Single().DepartmentId,
            Password = "TestPass123",
            RoleIds = new[] { supervisorRole.Id }
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
            FollowUpIntervalDays = 14,
            Name = Unique("测试项目"),
            OwnerId = 1,
            PlannedFinishDate = new DateTime(2026, 7, 29),
            ProgressCode = "testing",
            ProjectTypeCode = "prototype",
            StartDate = new DateTime(2026, 6, 29)
        })).Data!;

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static string UniqueThirdLevelCodeSeg()
        => Guid.NewGuid().ToString("N")[..3].ToUpperInvariant();
}
