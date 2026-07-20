using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    [HasPermission("user:view")]
    public async Task<ApiResult<PagedResult<UserDto>>> List(
        string? keyword,
        int page = 1,
        int pageSize = 20,
        int? departmentId = null,
        int? roleId = null)
        => ApiResult<PagedResult<UserDto>>.Ok(await _rbac.GetUsersAsync(keyword, page, pageSize, departmentId, roleId));

    [HttpGet("options")]
    [Authorize]
    public async Task<ApiResult<PagedResult<UserOptionDto>>> Options(
        string? keyword = null,
        int page = 1,
        int pageSize = 50)
    {
        var permissions = User.FindAll("perm").Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        var allowedPermissions = new[]
        {
            "approval:create",
            "material-flow:transfer",
            "project:create",
            "project:edit",
            "project:view",
            "material:create",
            "material:edit",
            "asset:create",
            "asset:edit",
            "department:create",
            "department:edit",
            "report:view"
        };
        if (!allowedPermissions.Any(permissions.Contains))
        {
            throw new BizException(4030, "无权读取用户选项");
        }

        return ApiResult<PagedResult<UserOptionDto>>.Ok(await _rbac.GetActiveUserOptionsAsync(keyword, page, pageSize));
    }

    [HttpGet("approver-options")]
    [HasPermission("approval:add-sign")]
    public async Task<ApiResult<List<UserOptionDto>>> ApproverOptions(string? keyword = null)
        => ApiResult<List<UserOptionDto>>.Ok(await _rbac.GetActiveSupervisorOptionsAsync(keyword));

    [HttpPost]
    [HasPermission("user:create")]
    public async Task<ApiResult<UserDto>> Create(CreateUserRequest request)
        => ApiResult<UserDto>.Ok(await _rbac.CreateUserAsync(request, CanAssignRole()));

    [HttpGet("import/template")]
    [HasPermission("user:create")]
    public async Task<FileContentResult> ImportTemplate()
        => File(
            await _rbac.BuildUserImportTemplateAsync(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "user-import-template.xlsx");

    [HttpPost("import")]
    [HasPermission("user:create")]
    public async Task<ApiResult<UserImportResultDto>> Import(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var result = await _rbac.ImportUsersAsync(stream, CanAssignRole());
        return result.FailedCount > 0
            ? new ApiResult<UserImportResultDto>
            {
                Code = 4001,
                Message = "导入数据存在错误，请修正后重新导入",
                Data = result
            }
            : ApiResult<UserImportResultDto>.Ok(result);
    }

    [HttpPost("import/validate")]
    [HasPermission("user:create")]
    public async Task<ApiResult<UserImportResultDto>> ValidateImport(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return ApiResult<UserImportResultDto>.Ok(await _rbac.ValidateUserImportAsync(stream));
    }

    [HttpPut("{id:int}")]
    [HasPermission("user:edit")]
    public async Task<ApiResult<UserDto>> Update(int id, UpdateUserRequest request)
        => ApiResult<UserDto>.Ok(await _rbac.UpdateUserAsync(id, request, CurrentUserId(), CanAssignRole()));

    [HttpDelete("{id:int}")]
    [HasPermission("user:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _rbac.DeleteUserAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/reset-password")]
    [HasPermission("user:reset-password")]
    public async Task<ApiResult<object?>> ResetPassword(int id)
    {
        await _rbac.ResetPasswordAsync(id);
        return ApiResult.Ok();
    }

    [HttpPost("{id:int}/toggle-status")]
    [HasPermission("user:toggle-status")]
    public async Task<ApiResult<object?>> ToggleStatus(int id, SetUserStatusRequest? request)
    {
        await _rbac.ToggleUserStatusAsync(id, request?.IsActive);
        return ApiResult.Ok();
    }

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private bool CanAssignRole()
        => User.FindAll("perm").Any(x => x.Value == "user:assign-role");
}

