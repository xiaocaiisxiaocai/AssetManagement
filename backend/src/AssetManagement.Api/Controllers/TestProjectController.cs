using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
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
    [HasPermission("project:view")]
    public async Task<ApiResult<TestProjectStatsDto>> Stats()
        => ApiResult<TestProjectStatsDto>.Ok(await _service.GetStatsAsync());

    [HttpGet]
    [HasPermission("project:view")]
    public async Task<ApiResult<List<TestProjectDto>>> List([FromQuery] string? deleteStatus)
        => ApiResult<List<TestProjectDto>>.Ok(await _service.ListAsync(deleteStatus, CurrentUserId()));

    [HttpGet("page")]
    [HasPermission("project:view")]
    public async Task<ApiResult<PagedResult<TestProjectDto>>> ListPage([FromQuery] TestProjectPageQuery query)
        => ApiResult<PagedResult<TestProjectDto>>.Ok(await _service.ListPageAsync(query, CurrentUserId()));

    [HttpGet("options")]
    [HasPermission("project:view")]
    public async Task<ApiResult<List<TestProjectOptionDto>>> Options([FromQuery] string? kind)
        => ApiResult<List<TestProjectOptionDto>>.Ok(await _service.ListOptionsAsync(kind));

    [HttpPost("options")]
    [HasPermission("project:option")]
    public async Task<ApiResult<TestProjectOptionDto>> CreateOption(SaveTestProjectOptionRequest request)
        => ApiResult<TestProjectOptionDto>.Ok(await _service.CreateOptionAsync(request));

    [HttpPut("options/{id:int}")]
    [HasPermission("project:option")]
    public async Task<ApiResult<TestProjectOptionDto>> UpdateOption(int id, SaveTestProjectOptionRequest request)
        => ApiResult<TestProjectOptionDto>.Ok(await _service.UpdateOptionAsync(id, request));

    [HttpDelete("options/{id:int}")]
    [HasPermission("project:option")]
    public async Task<ApiResult<object?>> DeleteOption(int id)
    {
        await _service.DeleteOptionAsync(id);
        return ApiResult.Ok();
    }

    [HttpGet("{id:int}/followups")]
    [HasPermission("project:view")]
    public async Task<ApiResult<List<TestProjectFollowupDto>>> Followups(int id)
        => ApiResult<List<TestProjectFollowupDto>>.Ok(await _service.ListFollowupsAsync(id));

    [HttpPost("{id:int}/followups")]
    // 落地跟进按项目进度和负责人关系授权，控制器只拦截未登录用户。
    [Authorize]
    public async Task<ApiResult<TestProjectFollowupDto>> CreateFollowup(int id, SaveTestProjectFollowupRequest request)
        => ApiResult<TestProjectFollowupDto>.Ok(await _service.CreateFollowupAsync(id, request, CurrentUserId()));

    [HttpPut("{id:int}/followups/{followupId:int}")]
    // 具体写入权限由 Service 判断，避免固定权限码绕开“仅负责人可跟进”的业务规则。
    [Authorize]
    public async Task<ApiResult<TestProjectFollowupDto>> UpdateFollowup(int id, int followupId, SaveTestProjectFollowupRequest request)
        => ApiResult<TestProjectFollowupDto>.Ok(await _service.UpdateFollowupAsync(id, followupId, request, CurrentUserId()));

    [HttpDelete("{id:int}/followups/{followupId:int}")]
    // 删除跟进记录与新增、编辑保持同一套负责人/管理员业务授权。
    [Authorize]
    public async Task<ApiResult<object?>> DeleteFollowup(int id, int followupId)
    {
        await _service.DeleteFollowupAsync(id, followupId, CurrentUserId());
        return ApiResult.Ok();
    }

    [HttpPost]
    [HasPermission("project:create")]
    public async Task<ApiResult<TestProjectDto>> Create(SaveTestProjectRequest request)
        => ApiResult<TestProjectDto>.Ok(await _service.CreateAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("project:edit")]
    public async Task<ApiResult<TestProjectDto>> Update(int id, SaveTestProjectRequest request)
        => ApiResult<TestProjectDto>.Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("project:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/restore")]
    [HasPermission("project:restore")]
    public async Task<ApiResult<object?>> Restore(int id)
    {
        await _service.RestoreAsync(id);
        return ApiResult.Ok();
    }

    [HttpDelete("{id:int}/purge")]
    [HasPermission("project:purge")]
    public async Task<ApiResult<object?>> Purge(int id)
    {
        await _service.PurgeAsync(id);
        return ApiResult.Ok();
    }

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
