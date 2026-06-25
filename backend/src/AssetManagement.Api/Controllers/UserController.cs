using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IRbacService _rbac;

    public UserController(IRbacService rbac)
    {
        _rbac = rbac;
    }

    [HttpGet]
    [HasPermission("admin:user")]
    public async Task<ApiResult<PagedResult<UserDto>>> List(string? keyword, int page = 1, int pageSize = 20)
        => ApiResult<PagedResult<UserDto>>.Ok(await _rbac.GetUsersAsync(keyword, page, pageSize));

    [HttpPost]
    [HasPermission("admin:user")]
    public async Task<ApiResult<UserDto>> Create(CreateUserRequest request)
        => ApiResult<UserDto>.Ok(await _rbac.CreateUserAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("admin:user")]
    public async Task<ApiResult<UserDto>> Update(int id, UpdateUserRequest request)
        => ApiResult<UserDto>.Ok(await _rbac.UpdateUserAsync(id, request));

    [HttpPost("{id:int}/reset-password")]
    [HasPermission("admin:user")]
    public async Task<ApiResult<object?>> ResetPassword(int id)
    {
        await _rbac.ResetPasswordAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/toggle-status")]
    [HasPermission("admin:user")]
    public async Task<ApiResult<object?>> ToggleStatus(int id, SetUserStatusRequest? request)
    {
        await _rbac.ToggleUserStatusAsync(id, request?.IsActive);
        return ApiResult.Ok();
    }
}

