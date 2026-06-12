using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/menus")]
public class RbacMenuController : ControllerBase
{
    private readonly IRbacService _rbac;

    public RbacMenuController(IRbacService rbac)
    {
        _rbac = rbac;
    }

    [HttpGet]
    [HasPermission("admin:role")]
    public async Task<ApiResult<List<MenuDto>>> List()
        => ApiResult<List<MenuDto>>.Ok(await _rbac.GetMenusAsync());

    [HttpPost]
    [HasPermission("admin:role")]
    public async Task<ApiResult<MenuDto>> Create(MenuDto request)
        => ApiResult<MenuDto>.Ok(await _rbac.CreateMenuAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<MenuDto>> Update(int id, MenuDto request)
        => ApiResult<MenuDto>.Ok(await _rbac.UpdateMenuAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _rbac.DeleteMenuAsync(id);
        return ApiResult.Ok();
    }
}

