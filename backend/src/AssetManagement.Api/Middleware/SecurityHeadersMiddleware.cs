namespace AssetManagement.Api.Middleware;

/// <summary>
/// 为所有响应附加基础安全响应头，作为纵深防御的一层：即便前端因未来引入的第三方
/// 依赖或富文本渲染意外触发 XSS，也能限制脚本的注入/执行面，降低 token（当前存
/// 于 localStorage）被窃取的概率；同时防止点击劫持、MIME 类型嗅探等常见风险。
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task Invoke(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var headers = ctx.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // 开发环境需要保留 Swagger UI 的内联脚本/样式，不设置严格 CSP，
            // 避免误报为功能缺陷。
            if (!_isDevelopment)
            {
                headers["Content-Security-Policy"] =
                    "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'";
            }
            return Task.CompletedTask;
        });
        await _next(ctx);
    }
}
