using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Assets;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Files;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.Assets;

public class FileApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public FileApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_image_then_fetch_returns_same_bytes()
    {
        await Login();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };

        var url = await UploadImage(bytes, "photo.png", "image/png");

        url.Should().StartWith("/api/files/");
        var fetched = await _client.GetAsync(url);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await fetched.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task Upload_rejects_non_image_extension()
    {
        await Login();
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "malware.exe");

        var res = await _client.PostAsync("/api/files/upload", form);
        var body = await res.Content.ReadFromJsonAsync<ApiResult<JsonElement>>();

        body!.Code.Should().NotBe(0);
    }

    [Fact]
    public async Task Upload_rejects_spoofed_image_extension()
    {
        await Login();
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent("not a png"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "spoofed.png");

        var res = await _client.PostAsync("/api/files/upload", form);
        var body = await res.Content.ReadFromJsonAsync<ApiResult<JsonElement>>();

        body!.Code.Should().Be(4150);
        body.Message.Should().Contain("内容");
    }

    [Fact]
    public async Task Upload_uses_attachment_max_mb_system_setting()
    {
        await Login();
        await _client.PutAsJsonAsync("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "attachment_max_mb",
                Value = "1",
                Description = "附件大小限制 MB"
            }
        });
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(new byte[1024 * 1024 + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "large.png");

        var res = await _client.PostAsync("/api/files/upload", form);
        var body = await res.Content.ReadFromJsonAsync<ApiResult<JsonElement>>();

        body!.Code.Should().NotBe(0);
        body.Message.Should().Contain("1MB");
    }

    [Fact]
    public async Task Asset_rejects_external_or_unmanaged_image_url()
    {
        await Login();
        var categoryResponse = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Guid.NewGuid().ToString("N")[..6],
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();

        var response = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "非法图片地址资产",
            CategoryId = category!.Data!.Id,
            Images = new List<string> { "https://attacker.example/tracker.png" },
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<JsonElement>>();

        body!.Code.Should().Be(4152);
        body.Message.Should().Contain("本系统上传");
    }

    [Fact]
    public async Task Asset_rejects_well_formed_but_missing_stored_image()
    {
        await Login();
        var categoryResponse = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Guid.NewGuid().ToString("N")[..6],
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();

        var response = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "不存在图片资产",
            CategoryId = category!.Data!.Id,
            Images = new List<string> { $"/api/files/{Guid.NewGuid():N}.png" },
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<JsonElement>>();

        body!.Code.Should().Be(4152);
        body.Message.Should().Contain("不存在");
    }

    [Fact]
    public async Task Asset_image_reference_uses_the_same_trimmed_url_as_persistence()
    {
        await Login();
        var uploadedUrl = await UploadImage(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 },
            "trimmed-reference.png",
            "image/png");
        var categoryResponse = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = Guid.NewGuid().ToString("N")[..6],
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<ApiResult<CategoryNodeDto>>();

        var response = await _client.PostAsJsonAsync("/api/assets", new CreateAssetRequest
        {
            Name = "图片地址规范化资产",
            CategoryId = category!.Data!.Id,
            Images = new List<string> { $"  {uploadedUrl}  " },
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<AssetDto>>();

        body!.Code.Should().Be(0, body.Message);
        body.Data!.Images.Should().Equal(uploadedUrl);
    }

    [Fact]
    public async Task File_storage_with_trailing_separator_opens_only_valid_guid_image_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-file-root", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fileName = $"{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(root, fileName),
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        try
        {
            using var scope = _factory.Services.CreateScope();
            var service = new FileStorageService(root + Path.DirectorySeparatorChar, root,
                scope.ServiceProvider.GetRequiredService<AppDbContext>());

            var stored = service.Open(fileName);
            stored.Should().NotBeNull();
            using var stream = stored!.Stream;
            service.Open("readme.png").Should().BeNull();
            service.Open("../" + fileName).Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<string> UploadImage(byte[] bytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        var res = await _client.PostAsync("/api/files/upload", form);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResult<JsonElement>>();
        return body!.Data.GetProperty("url").GetString()!;
    }

    private async Task Login()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
    }
}
