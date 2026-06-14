using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Assets;

public class AssetApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AssetApiTests(WebApplicationFactory<Program> factory)
    {
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
            Model = "TBS1102C",
            Brand = "Tektronix",
            Price = 3000
        });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?categoryId={category.Id}");

        created.Data!.AssetNo.Should().Be($"{category.Code}-001");
        list!.Data!.Items.Should().Contain(x => x.Id == created.Data.Id && x.AssetNo == created.Data.AssetNo);
    }

    [Fact]
    public async Task Department_filter_includes_child_department_assets()
    {
        await Login();
        var category = await CreateCategory();
        var parent = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "制造中心",
            Code = Unique("D")
        });
        var child = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ParentId = parent.Data!.Id,
            Name = "装配组",
            Code = Unique("D-C")
        });

        var created = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "电动螺丝刀",
            CategoryId = category.Id,
            DepartmentId = child.Data!.Id,
            Price = 500
        });
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<AssetDto>>>($"/api/assets?departmentId={parent.Data.Id}");

        list!.Data!.Items.Should().Contain(x => x.Id == created.Data!.Id);
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
            Price = 120
        });
        await Put<ApiResult<AssetDto>>($"/api/assets/{created.Data!.Id}", new UpdateAssetRequest
        {
            Name = created.Data.Name,
            CategoryId = category.Id,
            Price = created.Data.Price,
            Quantity = 1,
            Status = AssetStatus.Borrowed
        });

        var res = await _client.DeleteAsync($"/api/assets/{created.Data.Id}");
        var body = await res.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4092);
    }

    [Fact]
    public async Task Import_validate_previews_errors_and_confirm_imports_valid_rows()
    {
        await Login();
        var category = await CreateCategory();
        var bytes = BuildXlsx(new[]
        {
            new[] { "名称", "分类编码", "型号", "品牌", "单价" },
            new[] { "万用表", category.Code, "UT61E", "UNI-T", "199.50" },
            new[] { "无效资产", "NO-SUCH-CAT", "X", "Demo", "20" }
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

    private async Task<CategoryNodeDto> CreateCategory()
    {
        var rootSeg = Unique("CAT");
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            Name = "测试分类",
            CodeSeg = rootSeg
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            Name = "末级分类",
            CodeSeg = Unique("LEAF")
        });
        return child.Data!;
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

    private async Task<T> PostFile<T>(string url, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "assets.xlsx");
        var res = await _client.PostAsync(url, form);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
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
}
