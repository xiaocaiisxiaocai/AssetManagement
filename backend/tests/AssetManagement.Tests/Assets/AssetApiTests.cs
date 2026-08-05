using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Assets;

public class AssetApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public AssetApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_asset_autogenerates_no_and_lists_by_category()
    {
        await Login();
        var category = await CreateCategory();

        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "示波器",
            CategoryId = category.Id,
            PurchaseDate = new DateTime(2026, 7, 1),
            RegistrationTime = new DateTime(2026, 7, 13, 9, 30, 0),
            CurrentCondition = "正常使用",
            Remark = "首次入库登记",
        });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?categoryId={category.Id}");

        created.Data!.AssetNo.Should().Be($"{category.Code}-001");
        created.Data.PurchaseDate.Should().Be(new DateTime(2026, 7, 1));
        created.Data.RegistrationTime.Should().Be(new DateTime(2026, 7, 13));
        created.Data.CurrentCondition.Should().Be("正常使用");
        created.Data.Remark.Should().Be("首次入库登记");
        list!.Data!.Items.Should().Contain(x => x.Id == created.Data.Id && x.AssetNo == created.Data.AssetNo);
    }

    [Fact]
    public async Task Asset_keyword_search_matches_number_or_name()
    {
        await Login();
        var category = await CreateCategory();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = $"关键字资产-{marker}",
            CategoryId = category.Id
        });

        var byName = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?keyword={Uri.EscapeDataString(marker)}&page=1&pageSize=20");
        var byNumber = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?keyword={Uri.EscapeDataString(created.Data!.AssetNo)}&page=1&pageSize=20");

        byName!.Data!.Items.Should().ContainSingle(x => x.Id == created.Data.Id);
        byNumber!.Data!.Items.Should().ContainSingle(x => x.Id == created.Data.Id);
    }

    [Fact]
    public async Task Create_asset_rejects_condition_outside_dictionary()
    {
        await Login();
        var category = await CreateCategory();

        var response = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "状况非法资产",
            CategoryId = category.Id,
            CurrentCondition = "随意填写的状况"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AssetDto>>();

        body!.Code.Should().Be(4001);
        body.Message.Should().Contain("不在数据字典中");
    }

    [Fact]
    public async Task Department_filter_includes_child_department_assets()
    {
        await Login();
        var category = await CreateCategory();
        var parent = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "制造中心"
        });
        var child = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ParentId = parent.Data!.Id,
            Name = "装配组"
        });

        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "电动螺丝刀",
            CategoryId = category.Id,
            DepartmentId = child.Data!.Id,
        });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?departmentId={parent.Data.Id}");

        list!.Data!.Items.Should().Contain(x => x.Id == created.Data!.Id);
    }

    [Fact]
    public async Task Create_asset_rejects_inactive_department()
    {
        await Login();
        var category = await CreateCategory();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = 1,
            Name = "已停用资产部门"
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{department.Data!.Id}", new UpdateDepartmentRequest
        {
            ManagerId = 1,
            Name = department.Data.Name,
            IsActive = false
        });

        var res = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "不应归属停用部门",
            CategoryId = category.Id,
            DepartmentId = department.Data.Id
        });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<AssetDto>>();

        body!.Code.Should().Be(4045);
        body.Message.Should().Be("部门不存在或已停用");
    }

    [Fact]
    public async Task Custodian_filter_returns_only_matching_assets()
    {
        await Login();
        var category = await CreateCategory();
        var matched = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "指定保管人资产",
            CategoryId = category.Id,
            CustodianId = 1,
        });
        var unmatched = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "未指定保管人资产",
            CategoryId = category.Id,
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?categoryId={category.Id}&custodianId=1");

        list!.Data!.Items.Should().ContainSingle(x => x.Id == matched.Data!.Id);
        list.Data.Items.Should().NotContain(x => x.Id == unmatched.Data!.Id);
    }

    [Fact]
    public async Task Exclude_custodian_filter_omits_matching_assets()
    {
        await Login();
        var category = await CreateCategory();
        var excluded = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "本人保管资产",
            CategoryId = category.Id,
            CustodianId = 1,
        });
        var included = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "其他可借资产",
            CategoryId = category.Id,
        });

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?categoryId={category.Id}&excludeCustodianId=1");

        list!.Data!.Items.Should().ContainSingle(x => x.Id == included.Data!.Id);
        list.Data.Items.Should().NotContain(x => x.Id == excluded.Data!.Id);
    }

    [Fact]
    public async Task Borrowed_asset_cannot_be_deleted()
    {
        await Login();
        var category = await CreateCategory();
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "借用中的资产",
            CategoryId = category.Id,
        });
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = created.Data!.Id,
            Reason = "借出后验证删除保护",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });
        Auth(await LoginToken("TEST-SUPERVISOR", "123456"));
        while (flow.Data!.Status == "pending")
        {
            flow = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
                new ApprovalActionRequest { NodeId = flow.Data.CurrentNodeIds.FirstOrDefault(), Opinion = "同意" });
        }

        await Login();
        var res = await _client.DeleteAsync($"/api/assets/{created.Data.Id}");
        var body = await res.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4092);
    }

    [Fact]
    public async Task Delete_asset_soft_deletes_then_purge_removes_it()
    {
        await Login();
        var category = await CreateCategory();
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "待软删除资产",
            CategoryId = category.Id,
        });
        var second = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "保留的第二项资产",
            CategoryId = category.Id,
        });
        second.Data!.AssetNo.Should().EndWith("-002");

        var purgeBeforeDelete = await _client.DeleteAsync($"/api/assets/{created.Data!.Id}/purge");
        var purgeBeforeDeleteBody = await purgeBeforeDelete.Content.ReadFromJsonAsync<ApiResult<object?>>();
        purgeBeforeDeleteBody!.Code.Should().Be(4097);

        var softDelete = await _client.DeleteAsync($"/api/assets/{created.Data.Id}");
        softDelete.EnsureSuccessStatusCode();
        var repeatedDelete = await _client.DeleteAsync($"/api/assets/{created.Data.Id}");
        var repeatedDeleteBody = await repeatedDelete.Content.ReadFromJsonAsync<ApiResult<object?>>();
        repeatedDeleteBody!.Code.Should().Be(4048);

        var normalList = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?categoryId={category.Id}");
        normalList!.Data!.Items.Should().NotContain(x => x.Id == created.Data.Id);

        var allList = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?deleteStatus=all&categoryId={category.Id}");
        allList!.Data!.Items.Should().ContainSingle(x => x.Id == created.Data.Id && x.IsDeleted);

        var deletedList = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?deletedOnly=true");
        deletedList!.Data!.Items.Should().ContainSingle(x => x.Id == created.Data.Id && x.IsDeleted);

        var deletedByStatus = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?deleteStatus=deleted");
        deletedByStatus!.Data!.Items.Should().ContainSingle(x => x.Id == created.Data.Id && x.IsDeleted);

        // 已删除资产仍可查看详情(供主清单中已删除行的"详情"按钮使用)
        var deletedDetail = await _client.GetFromJsonAsync<ApiResult<AssetDetailDto>>($"/api/assets/{created.Data.Id}/detail");
        deletedDetail!.Code.Should().Be(0);
        deletedDetail.Data!.Asset.IsDeleted.Should().BeTrue();

        var purge = await _client.DeleteAsync($"/api/assets/{created.Data.Id}/purge");
        purge.EnsureSuccessStatusCode();

        var deletedAfterPurge = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?deletedOnly=true");
        deletedAfterPurge!.Data!.Items.Should().NotContain(x => x.Id == created.Data.Id);

        var third = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "彻底删除中间项后新增",
            CategoryId = category.Id,
        });
        third.Data!.AssetNo.Should().EndWith("-003", "编号应按历史最大序号递增，不能按 COUNT 与保留项冲突");
    }

    [Fact]
    public async Task Soft_deleted_asset_can_be_restored()
    {
        await Login();
        var category = await CreateCategory();
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "待恢复资产",
            CategoryId = category.Id,
        });

        (await _client.DeleteAsync($"/api/assets/{created.Data!.Id}")).EnsureSuccessStatusCode();

        var restore = await _client.PostAsync($"/api/assets/{created.Data.Id}/restore", null);
        restore.EnsureSuccessStatusCode();

        // 恢复后回到未删除列表
        var normalList = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?categoryId={category.Id}");
        normalList!.Data!.Items.Should().ContainSingle(x => x.Id == created.Data.Id && !x.IsDeleted);

        // 未删除资产重复恢复应报错
        var restoreAgain = await _client.PostAsync($"/api/assets/{created.Data.Id}/restore", null);
        var restoreAgainBody = await restoreAgain.Content.ReadFromJsonAsync<ApiResult<object?>>();
        restoreAgainBody!.Code.Should().Be(4099);
    }

    [Fact]
    public async Task Category_with_assets_cannot_be_soft_deleted()
    {
        await Login();
        var category = await CreateCategory();
        await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "分类占用资产",
            CategoryId = category.Id,
        });

        var res = await _client.DeleteAsync($"/api/categories/{category.Id}");
        var body = await res.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4098);
    }

    [Fact]
    public async Task Export_includes_custodian_and_chinese_asset_status()
    {
        await Login();
        var category = await CreateCategory();
        var custodian = await CreateUser();
        var assetName = $"导出字段资产-{Guid.NewGuid():N}";
        await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = assetName,
            CategoryId = category.Id,
            CustodianId = custodian.Id
        });

        var response = await _client.GetAsync($"/api/assets/export?categoryId={category.Id}");
        response.EnsureSuccessStatusCode();
        var rows = ReadXlsxRows(await response.Content.ReadAsByteArrayAsync());

        rows[0].Should().Equal(
            "资产编号", "名称", "分类编码", "部门", "位置", "保管人", "数量", "状态",
            "购入日期", "资产登记日期", "目前状况", "备注");
        rows.Should().Contain(row =>
            row[1] == assetName && row[5] == custodian.Name && row[7] == "在库");
    }

    [Fact]
    public async Task Import_template_includes_all_asset_create_fields()
    {
        await Login();

        var response = await _client.GetAsync("/api/assets/import/template");
        response.EnsureSuccessStatusCode();
        var rows = ReadXlsxRows(await response.Content.ReadAsByteArrayAsync());

        rows[0].Should().Equal(
            "资产编号", "名称", "分类编码", "数量", "购入日期", "资产登记日期",
            "目前状况", "归属部门", "保管人", "存放位置", "备注");
        rows[1].Should().Equal(
            "ZC-SAMPLE-001", "示例资产（请替换）", "示例分类编码（请替换）", "1",
            "2026-01-01", "2026-01-02", "正常使用", "示例部门（请替换）",
            "张三（请替换）", "A区-01", "此行为填写范例，导入前请删除或替换");
    }

    [Fact]
    public async Task Import_validate_previews_errors_and_confirm_imports_valid_rows()
    {
        await Login();
        var category = await CreateCategory();
        var bytes = BuildXlsx(new[]
        {
            new[] { "名称", "分类编码" },
            new[] { "万用表", category.Code },
            new[] { "无效资产", "NO-SUCH-CAT" }
        });

        var preview = await PostFile<ApiResult<List<ImportPreviewRow>>>("/api/assets/import/validate", bytes);
        preview.Data!.Should().ContainSingle(x => x.IsValid);
        preview.Data!.Should().ContainSingle(x => !x.IsValid && x.Error.Contains("分类编码不存在"));

        var confirmed = await PostFile<ApiResult<ImportConfirmResult>>("/api/assets/import/confirm", bytes);
        confirmed.Data!.SuccessCount.Should().Be(1);
        confirmed.Data.FailedCount.Should().Be(1);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?categoryId={category.Id}&name=万用表");
        list!.Data!.Items.Should().ContainSingle(x => x.Name == "万用表");
    }

    [Fact]
    public async Task Import_confirm_persists_department_custodian_location_and_quantity()
    {
        await Login();
        var category = await CreateCategory();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("资产导入部门")
        });
        var custodian = await CreateUser(department.Data!.Id);
        var assetName = Unique("完整字段导入资产");
        var customAssetNo = $"CUSTOM-{Guid.NewGuid():N}";
        var bytes = BuildXlsx(new[]
        {
            new[]
            {
                "资产编号", "名称", "分类编码", "数量", "购入日期", "资产登记日期",
                "目前状况", "归属部门", "保管人", "存放位置", "备注"
            },
            new[]
            {
                customAssetNo, assetName, category.Code, "3", "2026-08-01", "2026-08-02",
                "正常使用", department.Data.Name, custodian.Name, "三楼研发区 A-12", "完整字段"
            }
        });

        var preview = await PostFile<ApiResult<List<ImportPreviewRow>>>("/api/assets/import/validate", bytes);
        var row = preview.Data!.Should().ContainSingle().Subject;
        row.IsValid.Should().BeTrue(row.Error);
        row.DepartmentName.Should().Be(department.Data.Name);
        row.CustodianEmployeeNo.Should().Be(custodian.EmployeeNo);
        row.CustodianName.Should().Be(custodian.Name);
        row.LocationName.Should().Be("三楼研发区 A-12");
        row.Quantity.Should().Be(3);
        row.AssetNo.Should().Be(customAssetNo);

        var confirmed = await PostFile<ApiResult<ImportConfirmResult>>("/api/assets/import/confirm", bytes);
        confirmed.Data!.SuccessCount.Should().Be(1);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?categoryId={category.Id}&name={Uri.EscapeDataString(assetName)}");
        var asset = list!.Data!.Items.Should().ContainSingle().Subject;
        asset.AssetNo.Should().Be(customAssetNo);
        asset.DepartmentId.Should().Be(department.Data.Id);
        asset.DepartmentName.Should().Be(department.Data.Name);
        asset.CustodianId.Should().Be(custodian.Id);
        asset.CustodianName.Should().Be(custodian.Name);
        asset.LocationName.Should().Be("三楼研发区 A-12");
        asset.Quantity.Should().Be(3);
        asset.PurchaseDate.Should().Be(new DateTime(2026, 8, 1));
        asset.RegistrationTime.Should().Be(new DateTime(2026, 8, 2));
        asset.CurrentCondition.Should().Be("正常使用");
        asset.Remark.Should().Be("完整字段");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == asset.Id);
        stored.InitialCustodianId.Should().Be(custodian.Id);
    }

    [Fact]
    public async Task Import_preview_rejects_ambiguous_custodian_name_and_accepts_employee_number()
    {
        await Login();
        var category = await CreateCategory();
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = Unique("同名保管人部门")
        });
        var first = await CreateUser(department.Data!.Id);
        await CreateUser(department.Data.Id);
        var bytes = BuildXlsx(new[]
        {
            new[] { "资产编号", "名称", "分类编码", "数量", "保管人", "归属部门" },
            new[] { "", "姓名不唯一资产", category.Code, "1", first.Name, department.Data.Name },
            new[] { "", "工号唯一资产", category.Code, "1", first.EmployeeNo, department.Data.Name }
        });

        var preview = await PostFile<ApiResult<List<ImportPreviewRow>>>("/api/assets/import/validate", bytes);

        preview.Data.Should().HaveCount(2);
        preview.Data![0].IsValid.Should().BeFalse();
        preview.Data[0].Error.Should().Contain("保管人姓名不唯一，请填写工号");
        preview.Data[1].IsValid.Should().BeTrue(preview.Data[1].Error);
        preview.Data[1].CustodianId.Should().Be(first.Id);
        preview.Data[1].CustodianEmployeeNo.Should().Be(first.EmployeeNo);
    }

    [Fact]
    public async Task Import_preview_rejects_unknown_organization_values_and_invalid_extended_fields()
    {
        await Login();
        var category = await CreateCategory();
        var bytes = BuildXlsx(new[]
        {
            new[]
            {
                "名称", "分类编码", "购入日期", "资产登记日期", "目前状况", "备注",
                "归属部门", "保管人工号", "存放位置", "数量", "资产编号"
            },
            new[]
            {
                "无效扩展字段资产", category.Code, "", "", "", "",
                "不存在部门", "不存在工号", new string('位', 101), "0", new string('号', 101)
            }
        });

        var preview = await PostFile<ApiResult<List<ImportPreviewRow>>>("/api/assets/import/validate", bytes);
        var row = preview.Data!.Should().ContainSingle().Subject;
        row.IsValid.Should().BeFalse();
        row.Error.Should().Contain("部门名称不存在或已停用");
        row.Error.Should().Contain("保管人不存在或已停用");
        row.Error.Should().Contain("存放位置不能超过 100 个字符");
        row.Error.Should().Contain("数量必须是大于 0 的整数");
        row.Error.Should().Contain("资产编号不能超过 100 个字符");
    }

    [Fact]
    public async Task Import_preview_rejects_existing_and_in_file_duplicate_custom_asset_numbers()
    {
        await Login();
        var category = await CreateCategory();
        var existing = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "已有编号资产",
            CategoryId = category.Id
        });
        var duplicatedInFile = $"CUSTOM-{Guid.NewGuid():N}";
        var bytes = BuildXlsx(new[]
        {
            new[]
            {
                "名称", "分类编码", "购入日期", "资产登记日期", "目前状况", "备注",
                "归属部门", "保管人工号", "存放位置", "数量", "资产编号"
            },
            new[] { "文件重复一", category.Code, "", "", "", "", "", "", "", "1", duplicatedInFile },
            new[] { "文件重复二", category.Code, "", "", "", "", "", "", "", "1", duplicatedInFile.ToLowerInvariant() },
            new[] { "数据库重复", category.Code, "", "", "", "", "", "", "", "1", existing.Data!.AssetNo }
        });

        var preview = await PostFile<ApiResult<List<ImportPreviewRow>>>("/api/assets/import/validate", bytes);

        preview.Data.Should().HaveCount(3);
        preview.Data![0].Error.Should().Contain("资产编号在文件中重复");
        preview.Data[1].Error.Should().Contain("资产编号在文件中重复");
        preview.Data[2].Error.Should().Contain("资产编号已存在");
        preview.Data.Should().OnlyContain(row => !row.IsValid);
    }

    [Fact]
    public async Task Import_auto_number_skips_custom_number_reserved_in_same_file()
    {
        await Login();
        var category = await CreateCategory();
        var bytes = BuildXlsx(new[]
        {
            new[]
            {
                "名称", "分类编码", "购入日期", "资产登记日期", "目前状况", "备注",
                "归属部门", "保管人工号", "存放位置", "数量", "资产编号"
            },
            new[] { "自定义编号资产", category.Code, "", "", "", "", "", "", "", "1", $"{category.Code}-001" },
            new[] { "自动编号资产", category.Code, "", "", "", "", "", "", "", "1", "" }
        });

        var confirmed = await PostFile<ApiResult<ImportConfirmResult>>("/api/assets/import/confirm", bytes);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?categoryId={category.Id}&pageSize=20");

        confirmed.Data!.SuccessCount.Should().Be(2);
        list!.Data!.Items.Single(x => x.Name == "自定义编号资产").AssetNo.Should().Be($"{category.Code}-001");
        list.Data.Items.Single(x => x.Name == "自动编号资产").AssetNo.Should().Be($"{category.Code}-002");
    }

    [Fact]
    public async Task Import_preview_rejects_values_longer_than_database_columns()
    {
        await Login();
        var category = await CreateCategory();
        var bytes = BuildXlsx(new[]
        {
            new[] { "名称", "分类编码", "购入日期", "资产登记日期", "目前状况", "备注" },
            new[] { new string('名', 101), category.Code, "", "", "", "" },
            new[] { "合法名称", category.Code, "", "", "", new string('备', 501) },
        });

        var preview = await PostFile<ApiResult<List<ImportPreviewRow>>>("/api/assets/import/validate", bytes);

        preview.Data.Should().HaveCount(2);
        preview.Data![0].IsValid.Should().BeFalse();
        preview.Data[0].Error.Should().Contain("100");
        preview.Data[1].IsValid.Should().BeFalse();
        preview.Data[1].Error.Should().Contain("500");
    }

    [Fact]
    public async Task Category_counts_returns_direct_active_asset_counts()
    {
        await Login();
        var category = await CreateCategory();
        var active = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "分类计数资产",
            CategoryId = category.Id,
        });
        var deleted = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "已删除分类计数资产",
            CategoryId = category.Id,
        });
        await _client.DeleteAsync($"/api/assets/{deleted.Data!.Id}");

        var result = await _client.GetFromJsonAsync<ApiResult<Dictionary<int, int>>>(
            "/api/assets/category-counts");

        active.Data.Should().NotBeNull();
        result!.Data.Should().ContainKey(category.Id).WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task Import_uses_max_sequence_after_middle_asset_is_purged()
    {
        await Login();
        var category = await CreateCategory();
        var first = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "编号一",
            CategoryId = category.Id
        });
        var middle = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "编号二",
            CategoryId = category.Id
        });
        var third = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "编号三",
            CategoryId = category.Id
        });
        await _client.DeleteAsync($"/api/assets/{middle.Data!.Id}");
        await _client.DeleteAsync($"/api/assets/{middle.Data.Id}/purge");

        var bytes = BuildXlsx(new[]
        {
            new[] { "名称", "分类编码" },
            new[] { "编号四", category.Code }
        });
        var imported = await PostFile<ApiResult<ImportConfirmResult>>("/api/assets/import/confirm", bytes);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>(
            $"/api/assets?categoryId={category.Id}&pageSize=20");

        imported.Code.Should().Be(0, imported.Message);
        imported.Data!.SuccessCount.Should().Be(1);
        first.Data!.AssetNo.Should().EndWith("-001");
        third.Data!.AssetNo.Should().EndWith("-003");
        list!.Data!.Items.Single(x => x.Name == "编号四").AssetNo.Should().EndWith("-004");
    }

    [Fact]
    public async Task Create_asset_accepts_manual_location_and_rejects_invalid_custodian()
    {
        await Login();
        var category = await CreateCategory();

        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "手工位置资产",
            CategoryId = category.Id,
            LocationName = "  三楼研发区 A-12  "
        });

        var invalidCustodianResponse = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "无效保管人资产",
            CategoryId = category.Id,
            CustodianId = int.MaxValue
        });
        invalidCustodianResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        var invalidCustodian = await invalidCustodianResponse.Content.ReadFromJsonAsync<ApiResult<AssetDto>>();

        created.Data!.LocationName.Should().Be("三楼研发区 A-12");
        invalidCustodian!.Code.Should().Be(4041);
        invalidCustodian.Message.Should().Be("保管人不存在或已停用");
    }

    [Fact]
    public async Task Create_asset_rejects_location_longer_than_100_characters()
    {
        await Login();
        var category = await CreateCategory();

        var response = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "超长位置资产",
            CategoryId = category.Id,
            LocationName = new string('A', 101)
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_asset_persists_image_urls()
    {
        await Login();
        var category = await CreateCategory();
        var images = new List<string> { await UploadImage() };

        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "带照片的资产",
            CategoryId = category.Id,
            Images = images
        });
        var fetched = await _client.GetFromJsonAsync<ApiResult<AssetDto>>($"/api/assets/{created.Data!.Id}");

        created.Data!.Images.Should().Equal(images);
        fetched!.Data!.Images.Should().Equal(images);
    }

    [Fact]
    public async Task Asset_detail_returns_flows_and_recent_logs()
    {
        await Login();
        var category = await CreateCategory();
        var initialCustodian = await CreateUser();
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "详情资产",
            CategoryId = category.Id,
            CustodianId = initialCustodian.Id,
        });
        var id = created.Data!.Id;

        // 当前保管人会随借用/转让/归还变化，初始保管记录必须保持不变。
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Assets
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.CustodianId, (int?)null));
        }

        // 触发一次更新 → 产生带 TargetId 的资产审计日志
        await Put<ApiResult<AssetDto>>($"/api/assets/{id}", new UpdateAssetRequest
        {
            Name = "详情资产-改",
            CategoryId = category.Id,
            Quantity = 1,
            Status = AssetStatus.Available
        });
        // 发起借用 → 产生该资产的流转单
        await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = id,
            Reason = "详情测试借用",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        var detail = await _client.GetFromJsonAsync<ApiResult<AssetDetailDto>>($"/api/assets/{id}/detail");

        detail!.Data!.Asset.Id.Should().Be(id);
        detail.Data.Asset.CustodianId.Should().BeNull();
        detail.Data.InitialCustodianId.Should().Be(initialCustodian.Id);
        detail.Data.InitialCustodianName.Should().Be(initialCustodian.Name);
        detail.Data.Flows.Should().Contain(f => f.BizType == "borrow" && f.Applicant.Length > 0);
        detail.Data.RecentLogs.Should().Contain(l => l.ActionType == "PUT" && l.TargetId == id.ToString());
        detail.Data.RecentLogs.Should().Contain(l => l.TargetType == "Approval");
        typeof(AssetAuditLogDto).GetProperty("Detail").Should().BeNull();
        typeof(AssetAuditLogDto).GetProperty("Ip").Should().BeNull();
        typeof(AssetAuditLogDto).GetProperty("UserAgent").Should().BeNull();
    }

    [Fact]
    public async Task Update_asset_increments_concurrency_version()
    {
        await Login();
        var category = await CreateCategory();
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "并发版本资产",
            CategoryId = category.Id
        });

        uint before;
        using (var scope = _factory.Services.CreateScope())
        {
            before = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Assets
                .Where(x => x.Id == created.Data!.Id)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        await Put<ApiResult<AssetDto>>($"/api/assets/{created.Data!.Id}", new UpdateAssetRequest
        {
            Name = "并发版本资产-已更新",
            CategoryId = category.Id,
            Quantity = 1,
            Status = AssetStatus.Available
        });

        using var verifyScope = _factory.Services.CreateScope();
        var after = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Assets
            .Where(x => x.Id == created.Data.Id)
            .Select(x => x.RowVersion)
            .SingleAsync();
        after.Should().Be(before + 1);
    }

    [Fact]
    public async Task Asset_with_flow_history_cannot_be_purged()
    {
        await Login();
        var category = await CreateCategory();
        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "有流转历史资产",
            CategoryId = category.Id,
        });
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = created.Data!.Id,
            Reason = "保留历史",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });
        var pendingDelete = await _client.DeleteAsync($"/api/assets/{created.Data.Id}");
        var pendingDeleteBody = await pendingDelete.Content.ReadFromJsonAsync<ApiResult<object?>>();
        pendingDeleteBody!.Code.Should().Be(4094);
        pendingDeleteBody.Message.Should().Contain("待审批");

        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/withdraw", new { });
        await _client.DeleteAsync($"/api/assets/{created.Data.Id}");

        var purged = await _client.DeleteAsync($"/api/assets/{created.Data.Id}/purge");
        var body = await purged.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4094);
        body.Message.Should().Contain("资产存在流转历史");
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

    private async Task Login()
    {
        Auth(await LoginToken("1001", "123456"));
    }

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password
        });
        return body.Data!.Token;
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

    private async Task<AssetManagement.Application.Rbac.UserDto> CreateUser(int? departmentId = null)
    {
        var role = await _client.GetFromJsonAsync<ApiResult<AssetManagement.Application.Common.PagedResult<AssetManagement.Application.Rbac.RoleDto>>>("/api/roles?pageSize=100");
        var employeeRole = role!.Data!.Items.Single(x => x.Code == "employee");
        var user = await Post<ApiResult<AssetManagement.Application.Rbac.UserDto>>("/api/users", new AssetManagement.Application.Rbac.CreateUserRequest
        {
            EmployeeNo = Unique("u"),
            Name = "资产测试用户",
            DepartmentId = departmentId,
            RoleIds = new[] { employeeRole.Id }
        });
        return user.Data!;
    }

    private async Task<T> PostFile<T>(string url, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "assets.xlsx");
        var res = await _client.PostAsync(url, form);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<string> UploadImage()
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 });
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "asset.png");
        var response = await _client.PostAsync("/api/files/upload", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<AssetManagement.Application.Files.FileUploadResult>>())!
            .Data!.Url;
    }

    private static byte[] BuildXlsx(IEnumerable<string[]> rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Assets" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/worksheets/sheet1.xml", BuildSheetXml(rows));
        }

        return ms.ToArray();
    }

    private static List<string[]> ReadXlsxRows(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(
            zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var document = XDocument.Load(reader);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "row")
            .Select(row => row.Elements(ns + "c")
                .Select(cell => cell.Descendants(ns + "t").SingleOrDefault()?.Value ?? "")
                .ToArray())
            .ToList();
    }

    private static string BuildSheetXml(IEnumerable<string[]> rows)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetRows = rows.Select((cells, rowIndex) => new XElement(ns + "row",
            new XAttribute("r", rowIndex + 1),
            cells.Select((cell, colIndex) => new XElement(ns + "c",
                new XAttribute("r", $"{ColumnName(colIndex + 1)}{rowIndex + 1}"),
                new XAttribute("t", "inlineStr"),
                new XElement(ns + "is", new XElement(ns + "t", cell))))));
        return new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "worksheet", new XElement(ns + "sheetData", sheetRows))).ToString(SaveOptions.DisableFormatting);
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ColumnName(int index)
    {
        var name = "";
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }

        return name;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
