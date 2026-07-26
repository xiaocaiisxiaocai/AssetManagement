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

        // 这里只需要刷新声明，不应为每个请求构造完整且被跟踪的用户/RBAC 实体图。
        // 使用只读投影可显著减少传输列、对象分配和 ChangeTracker 压力，同时仍保持权限即时撤销语义。
        // 部门启用状态通过标量子查询一并取出，避免额外一次往返。
        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.EmployeeNo,
                x.DepartmentId,
                x.TokenVersion,
                DepartmentIsActive = x.DepartmentId.HasValue
                    && db.Departments.Any(d => d.Id == x.DepartmentId.Value && d.IsActive),
            })
            .SingleOrDefaultAsync();
        if (user is null)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, 4011, "账号不存在或已禁用");
            return;
        }

        if (!int.TryParse(context.User.FindFirstValue("tokenVersion"), out var tokenVersion)
            || tokenVersion != user.TokenVersion)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, 4014, "登录凭据已失效，请重新登录");
            return;
        }

        // 角色与权限合并为一次查询：按角色分组取出权限码，在内存中展开/去重，
        // 避免角色列表和权限列表各一次往返数据库。
        var roleData = await db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Role.IsActive)
            .Select(x => new
            {
                RoleCode = x.Role.Code,
                PermissionCodes = x.Role.RolePermissions.Select(rp => rp.Permission.Code).ToArray(),
            })
            .ToArrayAsync();
        var roleCodes = roleData.Select(x => x.RoleCode).Distinct().ToArray();
        if (roleCodes.Length == 0)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, 4012, "账号角色已禁用，请重新登录");
            return;
        }

        if (!roleCodes.Contains("admin", StringComparer.Ordinal)
            && roleCodes.Contains("supervisor", StringComparer.Ordinal)
            && !user.DepartmentIsActive)
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, 4013, "部门主管必须关联启用状态的部门");
            return;
        }

        var permissionCodes = roleData.SelectMany(x => x.PermissionCodes).Distinct().ToArray();

        RefreshPrincipal(context, user.Id, user.EmployeeNo, user.DepartmentId, user.TokenVersion, roleCodes,
            permissionCodes);

        await _next(context);
    }

    private static void RefreshPrincipal(
        HttpContext context,
        int userId,
        string employeeNo,
        int? departmentId,
        int tokenVersion,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var existingIdentity = context.User.Identity as ClaimsIdentity;
        var claims = context.User.Claims
            .Where(x => x.Type != ClaimTypes.Role && x.Type != "perm" && x.Type != "departmentId"
                && x.Type != ClaimTypes.NameIdentifier && x.Type != "employeeNo" && x.Type != "tokenVersion")
            .ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        claims.Add(new Claim("employeeNo", employeeNo));
        claims.Add(new Claim("tokenVersion", tokenVersion.ToString()));
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
