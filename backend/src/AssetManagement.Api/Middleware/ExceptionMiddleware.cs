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
            await Write(ctx, ex.HttpStatusCode ?? MapStatusCode(ex.Code), ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled");
            await Write(ctx, StatusCodes.Status500InternalServerError, 500, "服务器内部错误");
        }
    }

    // 与 MVC 管线保持一致的 camelCase 序列化,避免异常响应输出 PascalCase 导致前端拦截器读不到 code/message
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static int MapStatusCode(int code) => code switch
    {
        4004 => StatusCodes.Status404NotFound,
        400 or (>= 4000 and <= 4003) or (>= 4005 and <= 4009) => StatusCodes.Status400BadRequest,
        >= 4030 and <= 4039 => StatusCodes.Status403Forbidden,
        >= 4040 and <= 4049 => StatusCodes.Status404NotFound,
        4010 => StatusCodes.Status404NotFound,
        4016 => StatusCodes.Status403Forbidden,
        >= 4011 and <= 4015 => StatusCodes.Status409Conflict,
        4056 => StatusCodes.Status409Conflict,
        (>= 4050 and <= 4055) or (>= 4057 and <= 4059) => StatusCodes.Status422UnprocessableEntity,
        4060 => StatusCodes.Status404NotFound,
        >= 4061 and <= 4069 => StatusCodes.Status406NotAcceptable,
        >= 4090 and <= 4099 => StatusCodes.Status409Conflict,
        >= 4130 and <= 4139 => StatusCodes.Status413PayloadTooLarge,
        >= 4150 and <= 4159 => StatusCodes.Status415UnsupportedMediaType,
        >= 4290 and <= 4299 => StatusCodes.Status429TooManyRequests,
        500 or >= 5000 => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest,
    };

    private static async Task Write(HttpContext ctx, int statusCode, int code, string msg)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var body = JsonSerializer.Serialize(new ApiResult<object?> { Code = code, Message = msg }, JsonOptions);
        await ctx.Response.WriteAsync(body);
    }
}
