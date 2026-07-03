using AssetManagement.Application.Common;

namespace AssetManagement.Application.Rbac;

public interface IRbacService
{
    Task<PagedResult<UserDto>> GetUsersAsync(string? keyword, int page, int pageSize, int? departmentId = null, int? roleId = null);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request);
    Task DeleteUserAsync(int id);
    Task ResetPasswordAsync(int id);
    Task ToggleUserStatusAsync(int id, bool? isActive = null);
    Task<byte[]> BuildUserImportTemplateAsync();
    Task<UserImportResultDto> ValidateUserImportAsync(Stream file);
    Task<UserImportResultDto> ImportUsersAsync(Stream file);

    Task<PagedResult<RoleDto>> GetRolesAsync(int page, int pageSize);
    Task<RoleDto> GetRoleAsync(int id);
    Task<RoleDto> CreateRoleAsync(RoleDto request);
    Task<RoleDto> UpdateRoleAsync(int id, RoleDto request);
    Task DeleteRoleAsync(int id);
    Task<RoleDto> SetRolePermissionsAsync(int id, int[] permissionIds);
    Task<RoleDto> SetRoleMenusAsync(int id, int[] menuIds);

    Task<List<PermissionDto>> GetPermissionsAsync();
    Task<PermissionDto> CreatePermissionAsync(PermissionDto request);
    Task<PermissionDto> UpdatePermissionAsync(int id, PermissionDto request);
    Task DeletePermissionAsync(int id);

    Task<List<MenuDto>> GetMenusAsync();
    Task<MenuDto> CreateMenuAsync(MenuDto request);
    Task<MenuDto> UpdateMenuAsync(int id, MenuDto request);
    Task DeleteMenuAsync(int id);
}

