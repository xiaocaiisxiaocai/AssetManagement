using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/test-projects")]
public class TestProjectController : ControllerBase
{
    private readonly ITestProjectService _service;
    public TestProjectController(ITestProjectService service) => _service = service;

    [HttpGet]
    [HasPermission("material:view")]
    public async Task<ApiResult<List<TestProjectDto>>> List([FromQuery] string? deleteStatus)
        => ApiResult<List<TestProjectDto>>.Ok(await _service.ListAsync(deleteStatus));

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
}
