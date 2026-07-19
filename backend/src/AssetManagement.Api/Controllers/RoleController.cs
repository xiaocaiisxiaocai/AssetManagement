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
    [HasPermission("role:view")]
    public async Task<ApiResult<PagedResult<RoleDto>>> List(string? keyword = null, int page = 1, int pageSize = 20)
        => ApiResult<PagedResult<RoleDto>>.Ok(await _rbac.GetRolesAsync(keyword, page, pageSize));

    [HttpGet("{id:int}")]
    [HasPermission("role:view")]
    public async Task<ApiResult<RoleDto>> Get(int id)
        => ApiResult<RoleDto>.Ok(await _rbac.GetRoleAsync(id));

    [HttpPost]
    [HasPermission("role:create")]
    public async Task<ApiResult<RoleDto>> Create(CreateRoleRequest request)
        => ApiResult<RoleDto>.Ok(await _rbac.CreateRoleAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("role:edit")]
    public async Task<ApiResult<RoleDto>> Update(int id, UpdateRoleRequest request)
        => ApiResult<RoleDto>.Ok(await _rbac.UpdateRoleAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("role:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _rbac.DeleteRoleAsync(id);
        return ApiResult.Ok();
    }

    [HttpPut("{id:int}/permissions")]
    [HasPermission("role:assign-permission")]
    public async Task<ApiResult<RoleDto>> SetPermissions(int id, SetRolePermissionsRequest request)
        => ApiResult<RoleDto>.Ok(await _rbac.SetRolePermissionsAsync(id, request.PermissionIds));

    [HttpPut("{id:int}/menus")]
    [HasPermission("role:assign-menu")]
    public async Task<ApiResult<RoleDto>> SetMenus(int id, SetRoleMenusRequest request)
        => ApiResult<RoleDto>.Ok(await _rbac.SetRoleMenusAsync(id, request.MenuIds));

    [HttpPut("{id:int}/access")]
    [HasPermission("role:assign-permission")]
    [HasPermission("role:assign-menu")]
    public async Task<ApiResult<RoleDto>> SetAccess(int id, SetRoleAccessRequest request)
        => ApiResult<RoleDto>.Ok(await _rbac.SetRoleAccessAsync(id, request.PermissionIds, request.MenuIds));
}

