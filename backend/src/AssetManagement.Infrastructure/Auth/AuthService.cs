using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;

namespace AssetManagement.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(AppDbContext db, IJwtTokenService jwt, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _jwt = jwt;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var employeeNo = request.EmployeeNo.Trim();
        var clientIp = GetClientIp();

        // 检查账号锁定（工号维度）
        var accountKey = $"login_fail_account:{employeeNo}";
        if (_cache.TryGetValue(accountKey, out int accountFailCount) && accountFailCount >= AppConstants.MaxLoginAttempts)
        {
            throw new BizException(4291, $"账号已被锁定 {AppConstants.LoginLockoutMinutes} 分钟，请稍后再试");
        }

        // 检查 IP 锁定（IP 维度）
        var ipKey = $"login_fail_ip:{clientIp}";
        if (_cache.TryGetValue(ipKey, out int ipFailCount) && ipFailCount >= AppConstants.MaxLoginAttempts)
        {
            throw new BizException(4292, $"IP 地址已被锁定 {AppConstants.LoginLockoutMinutes} 分钟，请稍后再试");
        }

        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // 登录失败，记录失败次数
            RecordLoginFailure(accountKey, ipKey);
            throw new BizException(4011, "工号或密码错误");
        }

        // 登录成功，清除失败计数
        _cache.Remove(accountKey);
        _cache.Remove(ipKey);

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
            Token = _jwt.Create(user.Id, user.EmployeeNo, permissionCodes, roleCodes, user.DepartmentId)
        };
    }

    private void RecordLoginFailure(string accountKey, string ipKey)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AppConstants.LoginLockoutMinutes)
        };

        // 增加账号失败次数
        var accountCount = _cache.GetOrCreate(accountKey, entry =>
        {
            entry.SetOptions(cacheOptions);
            return 0;
        });
        _cache.Set(accountKey, accountCount + 1, cacheOptions);

        // 增加 IP 失败次数
        var ipCount = _cache.GetOrCreate(ipKey, entry =>
        {
            entry.SetOptions(cacheOptions);
            return 0;
        });
        _cache.Set(ipKey, ipCount + 1, cacheOptions);
    }

    private string GetClientIp()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return "unknown";

        // 优先从代理头获取真实 IP
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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
                    HideChildrenInMenu = menu.Name == "Home",
                    HideInMenu = menu.Name == "HomeWorkspace",
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
