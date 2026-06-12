using System.Text.Json;
using AssetManagement.Application.Common;

namespace AssetManagement.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (BizException ex)
        {
            await Write(ctx, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled");
            await Write(ctx, 500, "服务器内部错误");
        }
    }

    private static async Task Write(HttpContext ctx, int code, string msg)
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var body = JsonSerializer.Serialize(new ApiResult<object?> { Code = code, Message = msg });
        await ctx.Response.WriteAsync(body);
    }
}
