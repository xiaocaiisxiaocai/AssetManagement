using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

public class WorkflowCrudApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public WorkflowCrudApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Workflow_can_create_update_and_delete()
    {
        await Login();
        var bizType = Unique("custom");

        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "自定义流程",
            BizType = bizType
        });

        created.Code.Should().Be(0);
        created.Data!.Id.Should().BeGreaterThan(0);
        created.Data.Name.Should().Be("自定义流程");
        created.Data.BizType.Should().Be(bizType);

        var updated = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data.Id}", new SaveWorkflowRequest
        {
            Name = "自定义流程-已修改",
            BizType = $"{bizType}_edit"
        });

        updated.Code.Should().Be(0);
        updated.Data!.Name.Should().Be("自定义流程-已修改");
        updated.Data.BizType.Should().Be($"{bizType}_edit");

        var deleteResponse = await _client.DeleteAsync($"/api/workflows/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiResult<bool>>();
        deleted!.Code.Should().Be(0);
        deleted.Data.Should().BeTrue();

        var getDeleted = await _client.GetFromJsonAsync<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data.Id}");
        getDeleted!.Code.Should().Be(4049);
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
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];
}
