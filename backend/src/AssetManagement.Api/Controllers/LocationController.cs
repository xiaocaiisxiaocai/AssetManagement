using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationController : ControllerBase
{
    private readonly IBaseDataService _service;

    public LocationController(IBaseDataService service)
    {
        _service = service;
    }

    [HttpGet("tree")]
    [HasPermission("asset:view")]
    public async Task<ApiResult<List<LocationNodeDto>>> Tree()
        => ApiResult<List<LocationNodeDto>>.Ok(await _service.GetLocationTreeAsync());

    [HttpPost]
    [HasPermission("asset:create")]
    public async Task<ApiResult<LocationNodeDto>> Create(CreateLocationRequest request)
        => ApiResult<LocationNodeDto>.Ok(await _service.CreateLocationAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("asset:edit")]
    public async Task<ApiResult<LocationNodeDto>> Update(int id, UpdateLocationRequest request)
        => ApiResult<LocationNodeDto>.Ok(await _service.UpdateLocationAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("asset:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteLocationAsync(id);
        return ApiResult.Ok();
    }
}
