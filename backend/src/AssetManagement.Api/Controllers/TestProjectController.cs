using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/test-projects")]
public class TestProjectController : ControllerBase
{
    private readonly ITestProjectService _service;
    public TestProjectController(ITestProjectService service) => _service = service;

    [HttpGet("stats")]
    [HasPermission("material:view")]
    public async Task<ApiResult<TestProjectStatsDto>> Stats()
        => ApiResult<TestProjectStatsDto>.Ok(await _service.GetStatsAsync());

    [HttpGet]
    [HasPermission("material:view")]
    public async Task<ApiResult<List<TestProjectDto>>> List([FromQuery] string? deleteStatus)
        => ApiResult<List<TestProjectDto>>.Ok(await _service.ListAsync(deleteStatus, CurrentUserId()));

    [HttpGet("options")]
    [HasPermission("material:view")]
    public async Task<ApiResult<List<TestProjectOptionDto>>> Options([FromQuery] string? kind)
        => ApiResult<List<TestProjectOptionDto>>.Ok(await _service.ListOptionsAsync(kind));

    [HttpPost("options")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<TestProjectOptionDto>> CreateOption(SaveTestProjectOptionRequest request)
        => ApiResult<TestProjectOptionDto>.Ok(await _service.CreateOptionAsync(request));

    [HttpPut("options/{id:int}")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<TestProjectOptionDto>> UpdateOption(int id, SaveTestProjectOptionRequest request)
        => ApiResult<TestProjectOptionDto>.Ok(await _service.UpdateOptionAsync(id, request));

    [HttpDelete("options/{id:int}")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<object?>> DeleteOption(int id)
    {
        await _service.DeleteOptionAsync(id);
        return ApiResult.Ok();
    }

    [HttpGet("{id:int}/followups")]
    [HasPermission("material:view")]
    public async Task<ApiResult<List<TestProjectFollowupDto>>> Followups(int id)
        => ApiResult<List<TestProjectFollowupDto>>.Ok(await _service.ListFollowupsAsync(id));

    [HttpPost("{id:int}/followups")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<TestProjectFollowupDto>> CreateFollowup(int id, SaveTestProjectFollowupRequest request)
        => ApiResult<TestProjectFollowupDto>.Ok(await _service.CreateFollowupAsync(id, request, CurrentUserId()));

    [HttpPut("{id:int}/followups/{followupId:int}")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<TestProjectFollowupDto>> UpdateFollowup(int id, int followupId, SaveTestProjectFollowupRequest request)
        => ApiResult<TestProjectFollowupDto>.Ok(await _service.UpdateFollowupAsync(id, followupId, request, CurrentUserId()));

    [HttpDelete("{id:int}/followups/{followupId:int}")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<object?>> DeleteFollowup(int id, int followupId)
    {
        await _service.DeleteFollowupAsync(id, followupId, CurrentUserId());
        return ApiResult.Ok();
    }

    [HttpPost]
    [HasPermission("project:manage")]
    public async Task<ApiResult<TestProjectDto>> Create(SaveTestProjectRequest request)
        => ApiResult<TestProjectDto>.Ok(await _service.CreateAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<TestProjectDto>> Update(int id, SaveTestProjectRequest request)
        => ApiResult<TestProjectDto>.Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/restore")]
    [HasPermission("project:manage")]
    public async Task<ApiResult<object?>> Restore(int id)
    {
        await _service.RestoreAsync(id);
        return ApiResult.Ok();
    }

    [HttpDelete("{id:int}/purge")]
    [HasPermission("material:purge")]
    public async Task<ApiResult<object?>> Purge(int id)
    {
        await _service.PurgeAsync(id);
        return ApiResult.Ok();
    }

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
