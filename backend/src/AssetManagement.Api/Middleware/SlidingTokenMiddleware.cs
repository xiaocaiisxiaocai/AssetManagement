using System.Security.Claims;
using AssetManagement.Application.Auth;

namespace AssetManagement.Api.Middleware;

/// <summary>
/// Token 滑动续期:已认证请求若 token 剩余有效期不足配置时长的一半,
/// 用当前 claims 重签发新 token 并经 accesstoken 响应头下发(前端拦截器自动接收)。
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

        ctx.Response.Headers["accesstoken"] = jwt.Create(userId, employeeNo, perms, roles, departmentId);
    }
}
