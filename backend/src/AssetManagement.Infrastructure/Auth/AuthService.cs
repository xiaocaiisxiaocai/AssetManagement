using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var employeeNo = request.EmployeeNo.Trim();
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new BizException(4011, "工号或密码错误");
        }

        var activeRoles = user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsActive)
            .ToList();
        var roleCodes = activeRoles
            .Select(x => x.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var permissionCodes = activeRoles
            .SelectMany(x => x.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        return new LoginResponse
        {
            Token = _jwt.Create(user.Id, user.EmployeeNo, permissionCodes, roleCodes)
        };
    }

    public async Task<UserInfoDto> GetUserInfoAsync(int userId)
    {
        var user = await QueryActiveUser(userId)
            ?? throw new BizException(4041, "用户不存在或已停用");
        var roles = GetActiveRoles(user);

        return new UserInfoDto
        {
            Id = user.Id,
            Name = user.Name,
            EmployeeNo = user.EmployeeNo,
            Roles = roles.Select(x => x.Code).Distinct().OrderBy(x => x).ToArray(),
            Permissions = roles
                .SelectMany(x => x.RolePermissions)
                .Select(x => x.Permission.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToArray()
        };
    }

    public async Task<List<RouteDto>> GetRoutesAsync(int userId)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RoleMenus)
            .ThenInclude(x => x.Menu)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
        if (user is null)
        {
            throw new BizException(4041, "用户不存在或已停用");
        }

        var menus = GetActiveRoles(user)
            .SelectMany(x => x.RoleMenus)
            .Select(x => x.Menu)
            .Where(x => x.Type == "menu")
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToList();
        var buttonPermissions = await _db.Menus
            .Where(x => x.Type == "button" && x.PermissionCode != null)
            .ToListAsync();

        return BuildRoutes(null, menus, buttonPermissions);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive)
            ?? throw new BizException(4041, "用户不存在或已停用");

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
        {
            throw new BizException(1002, "旧密码不正确");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    private async Task<User?> QueryActiveUser(int userId)
        => await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);

    private static List<Role> GetActiveRoles(User user)
        => user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsActive)
            .ToList();

    private static List<RouteDto> BuildRoutes(int? parentId, List<Menu> menus, List<Menu> buttonPermissions)
        => menus
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .Select(menu => new RouteDto
            {
                Name = menu.Name,
                Path = menu.Path ?? "",
                Component = menu.Component ?? "",
                Meta = new RouteMetaDto
                {
                    Title = menu.Title,
                    Icon = menu.Icon,
                    Order = menu.Sort,
                    Permissions = buttonPermissions
                        .Where(x => x.ParentId == menu.Id)
                        .Select(x => x.PermissionCode!)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToArray()
                },
                Children = BuildRoutes(menu.Id, menus, buttonPermissions)
            })
            .ToList();
}
