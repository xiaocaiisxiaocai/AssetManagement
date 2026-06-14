using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class AssetCategoryController : ControllerBase
{
    private readonly IBaseDataService _service;

    public AssetCategoryController(IBaseDataService service)
    {
        _service = service;
    }

    [HttpGet("tree")]
    [HasPermission("asset:view")]
    public async Task<ApiResult<List<CategoryNodeDto>>> Tree()
        => ApiResult<List<CategoryNodeDto>>.Ok(await _service.GetCategoryTreeAsync());

    [HttpPost]
    [HasPermission("asset:create")]
    public async Task<ApiResult<CategoryNodeDto>> Create(CreateCategoryRequest request)
        => ApiResult<CategoryNodeDto>.Ok(await _service.CreateCategoryAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("asset:edit")]
    public async Task<ApiResult<CategoryNodeDto>> Update(int id, UpdateCategoryRequest request)
        => ApiResult<CategoryNodeDto>.Ok(await _service.UpdateCategoryAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("asset:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteCategoryAsync(id);
        return ApiResult.Ok();
    }
}
