using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Infrastructure.Audit;

public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "DELETE"
    };

    private static readonly Dictionary<string, string> ControllerEntityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Approval"] = nameof(ApprovalFlow),
        ["RbacMenu"] = nameof(Menu),
        ["Setting"] = nameof(SystemSetting)
    };

    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "Token",
        "BpmnXml"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppDbContext _db;

    public AuditActionFilter(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controllerName = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;
        var targetId = RouteValue(context, "id") ?? RouteValue(context, "assetId");
        var before = await TakeSnapshot(controllerName, targetId);

        var stopwatch = Stopwatch.StartNew();
        var executed = await next();
        stopwatch.Stop();
        if (!ShouldLog(context, executed))
        {
            return;
        }

        // Action 本身抛出了异常时跳过审计写入，避免在 faulted ChangeTracker 上二次提交
        if (executed.Exception != null && !executed.ExceptionHandled)
        {
            return;
        }

        var userIdText = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdText, out var value) ? value : null;
        // 从路由 {id} 捕获目标实体主键,便于按实体回溯其操作日志(如资产详情)
        targetId ??= ExtractResultId(executed);
        var after = await TakeSnapshot(controllerName, targetId);
        var success = IsSuccess(context, executed);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            ActionType = context.HttpContext.Request.Method,
            TargetType = controllerName,
            TargetId = targetId,
            Summary = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}",
            Detail = BuildDetail(context, executed, success, before, after),
            Ip = IpNormalizer.Normalize(context.HttpContext.Connection.RemoteIpAddress?.ToString()),
            UserAgent = Truncate(context.HttpContext.Request.Headers.UserAgent.ToString(), 500),
            DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(context.HttpContext.RequestAborted);
    }

    private static bool ShouldLog(ActionExecutingContext context, ActionExecutedContext executed)
        => WriteMethods.Contains(context.HttpContext.Request.Method);

    private static bool IsSuccess(ActionExecutingContext context, ActionExecutedContext executed)
        => executed.Exception is null
           && EffectiveStatusCode(context, executed) is >= 200 and < 300
           && BusinessCode(executed) == 0;

    private static int? BusinessCode(ActionExecutedContext executed)
    {
        if (executed.Result is not ObjectResult objectResult || objectResult.Value is null)
        {
            return null;
        }

        var codeProperty = objectResult.Value.GetType().GetProperty("Code");
        return codeProperty?.GetValue(objectResult.Value) as int?;
    }

    private async Task<Dictionary<string, object?>?> TakeSnapshot(string? controllerName, string? targetId)
    {
        if (string.IsNullOrWhiteSpace(controllerName) || string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        var entityTypeName = ControllerEntityMap.GetValueOrDefault(controllerName, controllerName);
        var entityType = _db.Model.GetEntityTypes()
            .FirstOrDefault(x => string.Equals(x.ClrType.Name, entityTypeName, StringComparison.OrdinalIgnoreCase));
        var key = entityType?.FindPrimaryKey();
        var keyProperty = key?.Properties.Count == 1 ? key.Properties[0] : null;
        if (entityType is null || keyProperty is null)
        {
            return null;
        }

        var keyValue = ConvertKey(targetId, keyProperty.ClrType);
        if (keyValue is null)
        {
            return null;
        }

        var entity = await QueryEntitySnapshot(entityType.ClrType, keyProperty.Name, keyValue);
        if (entity is null)
        {
            return null;
        }

        return entityType.GetProperties()
            .Where(x => !x.IsShadowProperty() && x.PropertyInfo is not null && !IsSensitive(x.Name))
            .OrderBy(x => x.Name)
            .ToDictionary(x => x.Name, x => NormalizeValue(x.PropertyInfo!.GetValue(entity)));
    }

    private async Task<object?> QueryEntitySnapshot(Type entityClrType, string keyPropertyName, object keyValue)
    {
        var method = typeof(AuditActionFilter)
            .GetMethod(nameof(QueryEntitySnapshotTyped), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .MakeGenericMethod(entityClrType);
        var task = (Task<object?>)method.Invoke(this, [keyPropertyName, keyValue])!;
        return await task;
    }

    private async Task<object?> QueryEntitySnapshotTyped<TEntity>(string keyPropertyName, object keyValue)
        where TEntity : class
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "x");
        var property = System.Linq.Expressions.Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [keyValue.GetType()],
            parameter,
            System.Linq.Expressions.Expression.Constant(keyPropertyName));
        var equals = System.Linq.Expressions.Expression.Equal(
            property,
            System.Linq.Expressions.Expression.Constant(keyValue));
        var predicate = System.Linq.Expressions.Expression.Lambda(equals, parameter);

        var typedPredicate = (System.Linq.Expressions.Expression<Func<TEntity, bool>>)predicate;
        return await _db.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(typedPredicate);
    }

    private static object? ConvertKey(string targetId, Type keyType)
    {
        var type = Nullable.GetUnderlyingType(keyType) ?? keyType;
        try
        {
            if (type == typeof(int) && int.TryParse(targetId, out var intValue))
            {
                return intValue;
            }

            if (type == typeof(long) && long.TryParse(targetId, out var longValue))
            {
                return longValue;
            }

            if (type == typeof(Guid) && Guid.TryParse(targetId, out var guidValue))
            {
                return guidValue;
            }

            return type == typeof(string) ? targetId : null;
        }
        catch
        {
            return null;
        }
    }

    private static object? NormalizeValue(object? value)
        => value switch
        {
            null => null,
            DateTime date => date.ToString("O"),
            Enum enumValue => enumValue.ToString(),
            _ => value
        };

    private static bool IsSensitive(string propertyName)
        => SensitiveProperties.Any(propertyName.Contains);

    private static string? RouteValue(ActionExecutingContext context, string key)
        => context.RouteData.Values.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? ExtractResultId(ActionExecutedContext executed)
    {
        if (executed.Result is not ObjectResult objectResult || objectResult.Value is null)
        {
            return null;
        }

        var data = objectResult.Value.GetType().GetProperty("Data")?.GetValue(objectResult.Value);
        var id = data?.GetType().GetProperty("Id")?.GetValue(data);
        return id?.ToString();
    }

    private static string BuildDetail(
        ActionExecutingContext context,
        ActionExecutedContext executed,
        bool success,
        Dictionary<string, object?>? before,
        Dictionary<string, object?>? after)
    {
        var detail = new
        {
            Success = success,
            StatusCode = EffectiveStatusCode(context, executed),
            ExceptionType = executed.Exception?.GetType().Name,
            Error = executed.Exception?.Message,
            Before = before,
            After = after,
            Changes = BuildChanges(before, after)
        };

        return JsonSerializer.Serialize(detail, JsonOptions);
    }

    private static int EffectiveStatusCode(ActionExecutingContext context, ActionExecutedContext executed)
        => executed.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? context.HttpContext.Response.StatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => context.HttpContext.Response.StatusCode
        };

    private static List<object> BuildChanges(Dictionary<string, object?>? before, Dictionary<string, object?>? after)
    {
        if (before is null && after is null)
        {
            return new List<object>();
        }

        var keys = before?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (after is not null)
        {
            keys.UnionWith(after.Keys);
        }

        return keys
            .Where(key => !Equals(before?.GetValueOrDefault(key), after?.GetValueOrDefault(key)))
            .OrderBy(key => key)
            .Select(key => new
            {
                Field = key,
                Before = before?.GetValueOrDefault(key),
                After = after?.GetValueOrDefault(key)
            })
            .Cast<object>()
            .ToList();
    }
}
