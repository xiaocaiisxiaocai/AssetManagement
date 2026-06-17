using System.Security.Claims;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AssetManagement.Infrastructure.Audit;

public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "DELETE"
    };

    private readonly AppDbContext _db;

    public AuditActionFilter(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (!ShouldLog(context, executed))
        {
            return;
        }

        var userIdText = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdText, out var value) ? value : null;
        var controllerName = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;
        // 从路由 {id} 捕获目标实体主键,便于按实体回溯其操作日志(如资产详情)
        var targetId = context.RouteData.Values.TryGetValue("id", out var idValue)
            ? idValue?.ToString()
            : null;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            ActionType = context.HttpContext.Request.Method,
            TargetType = controllerName,
            TargetId = targetId,
            Summary = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}",
            Ip = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(context.HttpContext.RequestAborted);
    }

    private static bool ShouldLog(ActionExecutingContext context, ActionExecutedContext executed)
        => executed.Exception is null
           && WriteMethods.Contains(context.HttpContext.Request.Method)
           && context.HttpContext.Response.StatusCode is >= 200 and < 300;
}

