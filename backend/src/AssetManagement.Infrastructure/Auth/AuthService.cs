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
    private static readonly object LoginFailureCounterLock = new();
    private static readonly string DummyPasswordHash = PasswordHashing.Hash("asset-management-dummy-password");
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

        // 密码校验前只读取定长凭据字段，避免已存在账号因加载角色/权限集合
        // 产生明显更多的数据库读取和实体物化，从而泄漏账号是否存在。
        var credential = await _db.Users
            .AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo)
            .Select(x => new { x.Id, x.PasswordHash, x.IsActive })
            .SingleOrDefaultAsync();

        // 不存在、禁用和密码错误走同一 BCrypt 与响应路径，避免通过消息或耗时枚举账号。
        var passwordMatches = PasswordHashing.Verify(request.Password, credential?.PasswordHash ?? DummyPasswordHash);
        if (credential is null || !passwordMatches || !credential.IsActive)
        {
            RecordLoginFailure(accountKey, ipKey);
            throw new BizException(4011, "工号或密码错误");
        }

        // 历史标准 bcrypt 哈希在首次成功登录时无感升级为带标记的 SHA-384 预哈希格式。
        // 条件更新避免覆盖并发发生的重置密码或改密结果。
        var verifiedPasswordHash = credential.PasswordHash;
        if (PasswordHashing.NeedsUpgrade(credential.PasswordHash))
        {
            var upgradedHash = PasswordHashing.Hash(request.Password);
            var upgraded = await _db.Users
                .Where(x => x.Id == credential.Id && x.PasswordHash == credential.PasswordHash && x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PasswordHash, upgradedHash));
            if (upgraded != 1)
            {
                RecordLoginFailure(accountKey, ipKey);
                throw new BizException(4011, "工号或密码错误");
            }
            verifiedPasswordHash = upgradedHash;
        }

        // 只有凭据通过后才加载授权集合。再次限定启用状态和刚验证的哈希，关闭两次查询之间
        // 账号被停用或密码被重置后，旧凭据仍签发一次令牌的窗口。
        var user = await _db.Users
            .AsSplitQuery()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == credential.Id
                && x.IsActive
                && x.PasswordHash == verifiedPasswordHash);
        if (user is null)
        {
            RecordLoginFailure(accountKey, ipKey);
            throw new BizException(4011, "工号或密码错误");
        }

        // 登录成功，清除失败计数
        lock (LoginFailureCounterLock)
        {
            _cache.Remove(accountKey);
            _cache.Remove(ipKey);
        }

        var activeRoles = user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsActive)
            .ToList();
        if (activeRoles.Count == 0)
        {
            throw new BizException(4012, "账号角色已禁用，请联系系统管理员");
        }
        if (!activeRoles.Any(x => x.Code == "admin")
            && activeRoles.Any(x => x.Code == "supervisor")
            && (!user.DepartmentId.HasValue
                || !await _db.Departments.AnyAsync(x => x.Id == user.DepartmentId.Value && x.IsActive)))
        {
            throw new BizException(4013, "所属部门已停用，请联系系统管理员");
        }

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
            Token = _jwt.Create(
                user.Id,
                user.EmployeeNo,
                permissionCodes,
                roleCodes,
                user.DepartmentId,
                user.TokenVersion)
        };
    }

    private void RecordLoginFailure(string accountKey, string ipKey)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(AppConstants.LoginLockoutMinutes)
        };

        IncrementFailureCount(_cache, accountKey, cacheOptions);
        IncrementFailureCount(_cache, ipKey, cacheOptions);
    }

    internal static int IncrementFailureCount(
        IMemoryCache cache,
        string key,
        MemoryCacheEntryOptions cacheOptions)
    {
        lock (LoginFailureCounterLock)
        {
            var next = cache.TryGetValue(key, out int current) ? current + 1 : 1;
            cache.Set(key, next, cacheOptions);
            return next;
        }
    }

    private string GetClientIp()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return "unknown";
        // ForwardedHeadersMiddleware 只会接受配置为可信代理的转发头；这里不能直接信任客户端伪造的 X-Forwarded-For。
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
            .AsSplitQuery()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RoleMenus)
            .ThenInclude(x => x.Menu)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
        if (user is null)
        {
            throw new BizException(4041, "用户不存在或已停用");
        }

        var activeRoles = GetActiveRoles(user);
        var menus = activeRoles
            .SelectMany(x => x.RoleMenus)
            .Select(x => x.Menu)
            .Where(x => x.Type == "menu")
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToList();
        var ownedPermissionCodes = activeRoles
            .SelectMany(x => x.RolePermissions)
            .Select(x => x.Permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        var buttonPermissions = await _db.Menus
            .Where(x => x.Type == "button" && x.PermissionCode != null
                && ownedPermissionCodes.Contains(x.PermissionCode))
            .ToListAsync();

        return BuildRoutes(null, menus, buttonPermissions);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.AsTracking().FirstOrDefaultAsync(x => x.Id == userId && x.IsActive)
            ?? throw new BizException(4041, "用户不存在或已停用");

        if (!PasswordHashing.Verify(request.OldPassword, user.PasswordHash))
        {
            throw new BizException(1002, "旧密码不正确");
        }
        if (request.NewPassword == AppConstants.DefaultUserPassword)
        {
            throw new BizException(1003, "新密码不能使用系统默认密码");
        }
        PasswordPolicy.EnsureStrong(request.NewPassword);
        if (PasswordHashing.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new BizException(1005, "新密码不能与旧密码相同");
        }

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.TokenVersion++;
        await _db.SaveChangesAsync();
    }

    public async Task LogoutAsync(int userId)
    {
        var user = await _db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive)
            ?? throw new BizException(4041, "用户不存在或已停用");
        user.TokenVersion++;
        await _db.SaveChangesAsync();
    }

    private async Task<User?> QueryActiveUser(int userId)
        => await _db.Users
            .AsSplitQuery()
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
