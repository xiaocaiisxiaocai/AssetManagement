using AssetManagement.Application.Assets;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetController : ControllerBase
{
    private readonly IAssetService _service;

    public AssetController(IAssetService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission("asset:view")]
    public async Task<ApiResult<PagedResult<AssetDto>>> List([FromQuery] AssetQuery query)
        => ApiResult<PagedResult<AssetDto>>.Ok(await _service.QueryAsync(query));

    [HttpGet("{id:int}")]
    [HasPermission("asset:view")]
    public async Task<ApiResult<AssetDto>> Get(int id)
        => ApiResult<AssetDto>.Ok(await _service.GetAsync(id));

    [HttpGet("{id:int}/detail")]
    [HasPermission("asset:view")]
    public async Task<ApiResult<AssetDetailDto>> Detail(int id)
        => ApiResult<AssetDetailDto>.Ok(await _service.GetDetailAsync(id));

    [HttpPost]
    [HasPermission("asset:create")]
    public async Task<ApiResult<AssetDto>> Create(CreateAssetRequest request)
        => ApiResult<AssetDto>.Ok(await _service.CreateAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("asset:edit")]
    public async Task<ApiResult<AssetDto>> Update(int id, UpdateAssetRequest request)
        => ApiResult<AssetDto>.Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("asset:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return ApiResult.Ok();
    }

    [HttpDelete("{id:int}/purge")]
    [HasPermission("asset:purge")]
    public async Task<ApiResult<object?>> Purge(int id)
    {
        await _service.PurgeAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/restore")]
    [HasPermission("asset:restore")]
    public async Task<ApiResult<object?>> Restore(int id)
    {
        await _service.RestoreAsync(id);
        return ApiResult.Ok();
    }

    [HttpGet("export")]
    [HasPermission("asset:view")]
    public async Task<FileContentResult> Export([FromQuery] AssetQuery query)
        => File(await _service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "assets.xlsx");
}
