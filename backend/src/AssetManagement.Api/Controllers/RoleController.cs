using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/roles")]
public class RoleController : ControllerBase
{
    private readonly IRbacService _rbac;

    public RoleController(IRbacService rbac)
    {
        _rbac = rbac;
    }

    [HttpGet]
    [HasPermission("admin:role")]
    public async Task<ApiResult<PagedResult<RoleDto>>> List(int page = 1, int pageSize = 20)
        => ApiResult<PagedResult<RoleDto>>.Ok(await _rbac.GetRolesAsync(page, pageSize));

    [HttpGet("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<RoleDto>> Get(int id)
        => ApiResult<RoleDto>.Ok(await _rbac.GetRoleAsync(id));

    [HttpPost]
    [HasPermission("admin:role")]
    public async Task<ApiResult<RoleDto>> Create(RoleDto request)
        => ApiResult<RoleDto>.Ok(await _rbac.CreateRoleAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<RoleDto>> Update(int id, RoleDto request)
        => ApiResult<RoleDto>.Ok(await _rbac.UpdateRoleAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _rbac.DeleteRoleAsync(id);
        return ApiResult.Ok();
    }

    [HttpPut("{id:int}/permissions")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<RoleDto>> SetPermissions(int id, int[] permissionIds)
        => ApiResult<RoleDto>.Ok(await _rbac.SetRolePermissionsAsync(id, permissionIds));

    [HttpPut("{id:int}/menus")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<RoleDto>> SetMenus(int id, int[] menuIds)
        => ApiResult<RoleDto>.Ok(await _rbac.SetRoleMenusAsync(id, menuIds));
}

