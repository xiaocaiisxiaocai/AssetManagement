using System.Security.Claims;
using AssetManagement.Application.Auth;

namespace AssetManagement.Api.Middleware;

/// <summary>
/// Token 滑动续期:已认证请求若 token 剩余有效期不足配置时长的一半,
/// 用 AccountSecurityMiddleware 刚从数据库刷新的 claims 重签发新 token，
/// 并经 accesstoken 响应头下发(前端拦截器自动接收)。因此禁用账号、撤销权限和部门变更
/// 不会被旧 token 继续复制到续期 token 中。
/// 避免活跃用户在固定过期时间被强制登出。
/// </summary>
public class SlidingTokenMiddleware
{
    private readonly RequestDelegate _next;

    public SlidingTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx, IJwtTokenService jwt, IConfiguration config)
    {
        TryReissue(ctx, jwt, config);
        await _next(ctx);
    }

    private static void TryReissue(HttpContext ctx, IJwtTokenService jwt, IConfiguration config)
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!long.TryParse(user.FindFirst("exp")?.Value, out var expUnix))
        {
            return;
        }

        var remaining = DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow;
        var expireMinutes = int.TryParse(config["Jwt:ExpireMinutes"], out var m) ? m : 120;
        // 仍有效但剩余不足一半时才续期;已过期或仍很充足都不处理
        if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(expireMinutes / 2.0))
        {
            return;
        }

        if (!int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return;
        }

        var employeeNo = user.FindFirst("employeeNo")?.Value ?? "";
        var perms = user.FindAll("perm").Select(x => x.Value).ToArray();
        var roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        int? departmentId = int.TryParse(user.FindFirst("departmentId")?.Value, out var d) ? d : null;
        if (!int.TryParse(user.FindFirst("tokenVersion")?.Value, out var tokenVersion))
        {
            return;
        }

        // 绝对生命周期上限：即便持续活跃、token 不断被续期，登录会话超过上限后
        // 也不再续期，只能等当前 token 自然过期后重新登录，避免一次 token 泄露
        // 的影响窗口被滑动续期放大到无限期。
        var absoluteLifetimeHours = int.TryParse(config["Jwt:AbsoluteLifetimeHours"], out var h) ? h : 24;
        if (long.TryParse(user.FindFirst("sessionStartedAt")?.Value, out var sessionStartedAtUnix))
        {
            var sessionAge = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(sessionStartedAtUnix);
            if (sessionAge >= TimeSpan.FromHours(absoluteLifetimeHours))
            {
                return;
            }
        }
        else
        {
            sessionStartedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        ctx.Response.Headers["accesstoken"] = jwt.Create(
            userId,
            employeeNo,
            perms,
            roles,
            departmentId,
            tokenVersion,
            sessionStartedAtUnix);
    }
}
