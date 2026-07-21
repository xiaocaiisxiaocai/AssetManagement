using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/test-materials")]
public class TestMaterialController : ControllerBase
{
    private readonly ITestMaterialService _service;
    public TestMaterialController(ITestMaterialService service) => _service = service;

    [HttpGet]
    [HasPermission("material:view")]
    public async Task<ApiResult<PagedResult<TestMaterialDto>>> List([FromQuery] TestMaterialQuery query)
        => ApiResult<PagedResult<TestMaterialDto>>.Ok(await _service.QueryAsync(query));

    [HttpGet("{id:int}")]
    [HasPermission("material:view")]
    public async Task<ApiResult<TestMaterialDto>> Get(int id)
        => ApiResult<TestMaterialDto>.Ok(await _service.GetAsync(id));

    [HttpGet("{id:int}/detail")]
    [HasPermission("material:view")]
    public async Task<ApiResult<TestMaterialDetailDto>> Detail(int id)
        => ApiResult<TestMaterialDetailDto>.Ok(await _service.GetDetailAsync(id));

    [HttpPost]
    // 料件写入同时支持“料件权限码”和“项目负责人”两类入口，具体业务权限在 Service 中按项目判断。
    [Authorize]
    public async Task<ApiResult<TestMaterialDto>> Create(SaveTestMaterialRequest request)
        => ApiResult<TestMaterialDto>.Ok(await _service.CreateAsync(request));

    [HttpPut("{id:int}")]
    // 不能在控制器绑定固定权限码，否则项目负责人无法维护自己负责项目下的料件。
    [Authorize]
    public async Task<ApiResult<TestMaterialDto>> Update(int id, SaveTestMaterialRequest request)
        => ApiResult<TestMaterialDto>.Ok(await _service.UpdateAsync(id, request));

    [HttpPost("{id:int}/return")]
    [HasPermission("material:return")]
    public async Task<ApiResult<TestMaterialDto>> Return(int id)
        => ApiResult<TestMaterialDto>.Ok(await _service.ReturnToVendorAsync(id, CurrentUserId()));

    [HttpDelete("{id:int}")]
    [HasPermission("material:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/restore")]
    [HasPermission("material:restore")]
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
