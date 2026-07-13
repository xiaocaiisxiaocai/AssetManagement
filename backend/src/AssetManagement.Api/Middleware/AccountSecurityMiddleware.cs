using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Api.Middleware;

/// <summary>
/// 对每个已认证请求重新加载账号状态和 RBAC 授权，确保禁用账号、撤销角色/权限以及部门变更立即生效。
/// </summary>
public sealed class AccountSecurityMiddleware
{
    private readonly RequestDelegate _next;

    public AccountSecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, 4010, "登录状态无效，请重新登录");
            return;
        }

        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive);
        if (user is null)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, 4011, "账号不存在或已禁用");
            return;
        }

        var activeRoles = user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsActive)
            .ToList();
        if (activeRoles.Count == 0)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, 4012, "账号角色已禁用，请重新登录");
            return;
        }

        var roleCodes = activeRoles.Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray();
        if (!roleCodes.Contains("admin", StringComparer.Ordinal)
            && roleCodes.Contains("dept_admin", StringComparer.Ordinal)
            && (!user.DepartmentId.HasValue
                || !await db.Departments.AnyAsync(x => x.Id == user.DepartmentId.Value && x.IsActive)))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, 4013, "部门管理员必须关联启用状态的部门");
            return;
        }

        RefreshPrincipal(context, user.Id, user.EmployeeNo, user.DepartmentId, roleCodes,
            activeRoles.SelectMany(x => x.RolePermissions)
                .Select(x => x.Permission.Code)
                .Distinct(StringComparer.Ordinal));

        await _next(context);
    }

    private static void RefreshPrincipal(
        HttpContext context,
        int userId,
        string employeeNo,
        int? departmentId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var existingIdentity = context.User.Identity as ClaimsIdentity;
        var claims = context.User.Claims
            .Where(x => x.Type != ClaimTypes.Role && x.Type != "perm" && x.Type != "departmentId"
                && x.Type != ClaimTypes.NameIdentifier && x.Type != "employeeNo")
            .ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        claims.Add(new Claim("employeeNo", employeeNo));
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(permissions.Select(x => new Claim("perm", x)));
        if (departmentId.HasValue)
        {
            claims.Add(new Claim("departmentId", departmentId.Value.ToString()));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            existingIdentity?.AuthenticationType,
            existingIdentity?.NameClaimType ?? ClaimTypes.Name,
            existingIdentity?.RoleClaimType ?? ClaimTypes.Role));
    }

    private static async Task RejectAsync(HttpContext context, int statusCode, int code, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(ApiResult<object?>.Fail(code, message));
    }
}
