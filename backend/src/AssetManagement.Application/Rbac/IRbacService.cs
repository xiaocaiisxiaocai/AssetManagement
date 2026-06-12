using AssetManagement.Application.Common;

namespace AssetManagement.Application.Rbac;

public interface IRbacService
{
    Task<PagedResult<UserDto>> GetUsersAsync(string? keyword, int page, int pageSize);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request);
    Task ResetPasswordAsync(int id);
    Task ToggleUserStatusAsync(int id);

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

