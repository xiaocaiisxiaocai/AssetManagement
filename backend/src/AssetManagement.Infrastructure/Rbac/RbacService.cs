using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Rbac;

public class RbacService : IRbacService
{
    private readonly AppDbContext _db;

    public RbacService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(string? keyword, int page, int pageSize)
    {
        var query = _db.Users
            .Include(x => x.UserRoles)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(x => x.EmployeeNo.Contains(kw) || x.Name.Contains(kw));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToUserDto(x))
            .ToListAsync();

        return new PagedResult<UserDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        var password = !string.IsNullOrWhiteSpace(request.Password)
            ? request.Password
            : DefaultPassword(request.EmployeeNo);

        var user = new User
        {
            EmployeeNo = request.EmployeeNo.Trim(),
            Name = request.Name.Trim(),
            Email = request.Email,
            Phone = request.Phone,
            DepartmentId = request.DepartmentId,
            SupervisorId = request.SupervisorId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await RewriteUserRoles(user.Id, request.RoleIds);
        return await LoadUserDto(user.Id);
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new BizException(4041, "用户不存在");
        user.Name = request.Name.Trim();
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.DepartmentId = request.DepartmentId;
        user.SupervisorId = request.SupervisorId;
        await RewriteUserRoles(id, request.RoleIds);
        await _db.SaveChangesAsync();
        return await LoadUserDto(id);
    }

    public async Task ResetPasswordAsync(int id)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new BizException(4041, "用户不存在");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword(user.EmployeeNo));
        await _db.SaveChangesAsync();
    }

    public async Task ToggleUserStatusAsync(int id, bool? isActive = null)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new BizException(4041, "用户不存在");
        user.IsActive = isActive ?? !user.IsActive;
        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<RoleDto>> GetRolesAsync(int page, int pageSize)
    {
        var query = _db.Roles
            .Include(x => x.RolePermissions)
            .Include(x => x.RoleMenus)
            .OrderBy(x => x.Id);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToRoleDto(x)).ToListAsync();
        return new PagedResult<RoleDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<RoleDto> GetRoleAsync(int id) => await LoadRoleDto(id);

    public async Task<RoleDto> CreateRoleAsync(RoleDto request)
    {
        var role = new Role { Code = request.Code.Trim(), Name = request.Name.Trim(), IsActive = request.IsActive };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        await RewriteRolePermissions(role.Id, request.PermissionIds);
        await RewriteRoleMenus(role.Id, request.MenuIds);
        return await LoadRoleDto(role.Id);
    }

    public async Task<RoleDto> UpdateRoleAsync(int id, RoleDto request)
    {
        var role = await _db.Roles.FindAsync(id)
            ?? throw new BizException(4042, "角色不存在");
        role.Name = request.Name.Trim();
        role.IsActive = request.IsActive;
        await RewriteRolePermissions(id, request.PermissionIds);
        await RewriteRoleMenus(id, request.MenuIds);
        await _db.SaveChangesAsync();
        return await LoadRoleDto(id);
    }

    public async Task DeleteRoleAsync(int id)
    {
        var role = await _db.Roles.FindAsync(id)
            ?? throw new BizException(4042, "角色不存在");
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
    }

    public async Task<RoleDto> SetRolePermissionsAsync(int id, int[] permissionIds)
    {
        await RewriteRolePermissions(id, permissionIds);
        return await LoadRoleDto(id);
    }

    public async Task<RoleDto> SetRoleMenusAsync(int id, int[] menuIds)
    {
        await RewriteRoleMenus(id, menuIds);
        return await LoadRoleDto(id);
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
        => await _db.Permissions.OrderBy(x => x.Module).ThenBy(x => x.Code).Select(x => ToPermissionDto(x)).ToListAsync();

    public async Task<PermissionDto> CreatePermissionAsync(PermissionDto request)
    {
        var permission = new Permission { Code = request.Code.Trim(), Name = request.Name.Trim(), Module = request.Module };
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();
        return ToPermissionDto(permission);
    }

    public async Task<PermissionDto> UpdatePermissionAsync(int id, PermissionDto request)
    {
        var permission = await _db.Permissions.FindAsync(id)
            ?? throw new BizException(4043, "权限不存在");
        permission.Code = request.Code.Trim();
        permission.Name = request.Name.Trim();
        permission.Module = request.Module;
        await _db.SaveChangesAsync();
        return ToPermissionDto(permission);
    }

    public async Task DeletePermissionAsync(int id)
    {
        var permission = await _db.Permissions.FindAsync(id)
            ?? throw new BizException(4043, "权限不存在");
        _db.Permissions.Remove(permission);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MenuDto>> GetMenusAsync()
    {
        var menus = await _db.Menus.OrderBy(x => x.Sort).ThenBy(x => x.Id).ToListAsync();
        return BuildMenuTree(null, menus);
    }

    public async Task<MenuDto> CreateMenuAsync(MenuDto request)
    {
        var menu = new Menu
        {
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Title = request.Title.Trim(),
            Path = request.Path,
            Component = request.Component,
            Icon = request.Icon,
            Sort = request.Sort,
            Type = request.Type,
            PermissionCode = request.PermissionCode
        };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();
        return ToMenuDto(menu);
    }

    public async Task<MenuDto> UpdateMenuAsync(int id, MenuDto request)
    {
        var menu = await _db.Menus.FindAsync(id)
            ?? throw new BizException(4044, "菜单不存在");
        menu.ParentId = request.ParentId;
        menu.Name = request.Name.Trim();
        menu.Title = request.Title.Trim();
        menu.Path = request.Path;
        menu.Component = request.Component;
        menu.Icon = request.Icon;
        menu.Sort = request.Sort;
        menu.Type = request.Type;
        menu.PermissionCode = request.PermissionCode;
        await _db.SaveChangesAsync();
        return ToMenuDto(menu);
    }

    public async Task DeleteMenuAsync(int id)
    {
        var menu = await _db.Menus.FindAsync(id)
            ?? throw new BizException(4044, "菜单不存在");
        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync();
    }

    private async Task RewriteUserRoles(int userId, IEnumerable<int> roleIds)
    {
        _db.UserRoles.RemoveRange(_db.UserRoles.Where(x => x.UserId == userId));
        var distinctIds = roleIds.Distinct().ToArray();
        _db.UserRoles.AddRange(distinctIds.Select(roleId => new UserRole { UserId = userId, RoleId = roleId }));
        await _db.SaveChangesAsync();
    }

    private async Task RewriteRolePermissions(int roleId, IEnumerable<int> permissionIds)
    {
        if (!await _db.Roles.AnyAsync(x => x.Id == roleId))
        {
            throw new BizException(4042, "角色不存在");
        }

        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x => x.RoleId == roleId));
        _db.RolePermissions.AddRange(permissionIds.Distinct().Select(id => new RolePermission
        {
            RoleId = roleId,
            PermissionId = id
        }));
        await _db.SaveChangesAsync();
    }

    private async Task RewriteRoleMenus(int roleId, IEnumerable<int> menuIds)
    {
        if (!await _db.Roles.AnyAsync(x => x.Id == roleId))
        {
            throw new BizException(4042, "角色不存在");
        }

        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x => x.RoleId == roleId));
        _db.RoleMenus.AddRange(menuIds.Distinct().Select(id => new RoleMenu
        {
            RoleId = roleId,
            MenuId = id
        }));
        await _db.SaveChangesAsync();
    }

    private async Task<UserDto> LoadUserDto(int id)
    {
        var user = await _db.Users.Include(x => x.UserRoles).SingleAsync(x => x.Id == id);
        return ToUserDto(user);
    }

    private async Task<RoleDto> LoadRoleDto(int id)
    {
        var role = await _db.Roles
            .Include(x => x.RolePermissions)
            .Include(x => x.RoleMenus)
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");
        return ToRoleDto(role);
    }

    private static string DefaultPassword(string employeeNo)
        => employeeNo.Length <= 6 ? employeeNo.PadLeft(6, '0') : employeeNo[^6..];

    private static UserDto ToUserDto(User x) => new()
    {
        Id = x.Id,
        EmployeeNo = x.EmployeeNo,
        Name = x.Name,
        Email = x.Email,
        Phone = x.Phone,
        IsActive = x.IsActive,
        DepartmentId = x.DepartmentId,
        SupervisorId = x.SupervisorId,
        RoleIds = x.UserRoles.Select(r => r.RoleId).ToArray()
    };

    private static RoleDto ToRoleDto(Role x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        IsActive = x.IsActive,
        PermissionIds = x.RolePermissions.Select(p => p.PermissionId).ToArray(),
        MenuIds = x.RoleMenus.Select(m => m.MenuId).ToArray()
    };

    private static PermissionDto ToPermissionDto(Permission x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Module = x.Module
    };

    private static MenuDto ToMenuDto(Menu x) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        Title = x.Title,
        Path = x.Path,
        Component = x.Component,
        Icon = x.Icon,
        Sort = x.Sort,
        Type = x.Type,
        PermissionCode = x.PermissionCode
    };

    private static List<MenuDto> BuildMenuTree(int? parentId, List<Menu> menus)
        => menus
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToMenuDto(x);
                return dto with { Children = BuildMenuTree(x.Id, menus) };
            })
            .ToList();
}

