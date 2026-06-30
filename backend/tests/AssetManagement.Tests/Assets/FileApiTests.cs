using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using FluentAssertions;

namespace AssetManagement.Tests.Assets;

public class FileApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public FileApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_image_then_fetch_returns_same_bytes()
    {
        await Login();
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

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
