using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/permissions")]
public class PermissionController : ControllerBase
{
    private readonly IRbacService _rbac;

    public PermissionController(IRbacService rbac)
    {
        _rbac = rbac;
    }

    [HttpGet]
    [HasPermission("admin:role")]
    public async Task<ApiResult<List<PermissionDto>>> List()
        => ApiResult<List<PermissionDto>>.Ok(await _rbac.GetPermissionsAsync());

    [HttpPost]
    [HasPermission("admin:role")]
    public async Task<ApiResult<PermissionDto>> Create(PermissionDto request)
        => ApiResult<PermissionDto>.Ok(await _rbac.CreatePermissionAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<PermissionDto>> Update(int id, PermissionDto request)
        => ApiResult<PermissionDto>.Ok(await _rbac.UpdatePermissionAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("admin:role")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _rbac.DeletePermissionAsync(id);
        return ApiResult.Ok();
    }
}

