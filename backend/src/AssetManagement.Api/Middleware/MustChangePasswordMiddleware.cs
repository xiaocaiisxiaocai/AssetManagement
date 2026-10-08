using AssetManagement.Application.Common;

namespace AssetManagement.Api.Middleware;

/// <summary>
/// 非开发环境使用默认密码登录后，只放行改密、退出和读取当前用户，其余已认证接口返回 403。
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && string.Equals(context.User.FindFirst("mustChangePassword")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            && !IsAllowed(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResult<object?>.Fail(1006, "请先修改默认密码"));
            return;
        }

        await _next(context);
    }

    private static bool IsAllowed(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/auth/user-info", StringComparison.OrdinalIgnoreCase);
    }
}
