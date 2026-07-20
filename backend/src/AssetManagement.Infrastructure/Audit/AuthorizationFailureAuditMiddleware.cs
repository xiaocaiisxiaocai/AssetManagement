using System.Security.Claims;
using System.Text.Json;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Audit;

/// <summary>记录在 MVC ActionFilter 之前被授权中间件拒绝的写请求。</summary>
public sealed class AuthorizationFailureAuditMiddleware
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
    };

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthorizationFailureAuditMiddleware> _logger;

    public AuthorizationFailureAuditMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<AuthorizationFailureAuditMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
        if (context.Response.StatusCode != StatusCodes.Status403Forbidden
            || !WriteMethods.Contains(context.Request.Method))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdText, out var parsedUserId) ? parsedUserId : null;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                ActionType = $"{context.Request.Method}_denied",
                TargetType = context.Request.RouteValues.GetValueOrDefault("controller")?.ToString(),
                TargetId = context.Request.RouteValues.GetValueOrDefault("id")?.ToString(),
                Summary = Truncate($"权限拒绝：{context.Request.Method} {context.Request.Path}", 500)
                    ?? "权限拒绝",
                Detail = JsonSerializer.Serialize(new { Success = false, BusinessCode = 4030, Error = "没有操作权限" }),
                Ip = IpNormalizer.Normalize(context.Connection.RemoteIpAddress?.ToString()),
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 500),
                OccurredAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入权限拒绝审计失败：{Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
    }

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
