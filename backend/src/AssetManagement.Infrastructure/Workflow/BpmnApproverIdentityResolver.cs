using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

public enum ApproverIdentityResolutionStatus
{
    NotFound,
    Unique,
    Ambiguous,
}

public sealed record ApproverIdentityResolution(
    ApproverIdentityResolutionStatus Status,
    IReadOnlyList<int> UserIds,
    string Diagnostic)
{
    public bool IsResolved => Status == ApproverIdentityResolutionStatus.Unique;
}

/// <summary>
/// 解析 BPMN 用户和角色标识，历史值存在歧义时不静默选择其中一个。
/// </summary>
public static class BpmnApproverIdentityResolver
{
    private const string UserPrefix = "user:";
    private const string RolePrefix = "role:";

    public static async Task<ApproverIdentityResolution> ResolveUsersAsync(
        AppDbContext db,
        string identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var value = identity?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return NotFound("审批人标识为空");
        }

        if (value.StartsWith(UserPrefix, StringComparison.Ordinal))
        {
            var idText = value[UserPrefix.Length..];
            if (!TryParsePositiveId(idText, out var userId))
            {
                return NotFound($"审批人标识“{value}”不是有效的 user:<正整数> 格式");
            }

            var exists = await db.Users.AsNoTracking()
                .AnyAsync(x => x.IsActive && x.Id == userId, cancellationToken);
            return exists
                ? Unique([userId], $"审批人标识“{value}”已按用户 ID 精确解析")
                : NotFound($"审批人标识“{value}”未匹配启用用户");
        }

        if (value.Contains(':'))
        {
            return NotFound($"审批人标识“{value}”使用了不支持的结构化格式");
        }

        var hasLegacyId = TryParsePositiveId(value, out var legacyId);
        var userIds = await db.Users.AsNoTracking()
            .Where(x => x.IsActive &&
                (x.EmployeeNo == value || x.Name == value || (hasLegacyId && x.Id == legacyId)))
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return userIds.Count switch
        {
            0 => NotFound($"历史审批人标识“{value}”未匹配启用用户"),
            1 => Unique(userIds, $"历史审批人标识“{value}”唯一匹配启用用户"),
            _ => Ambiguous(userIds, $"历史审批人标识“{value}”同时匹配多个启用用户"),
        };
    }

    public static async Task<ApproverIdentityResolution> ResolveGroupUsersAsync(
        AppDbContext db,
        string groupIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var value = groupIdentity?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return NotFound("审批角色标识为空");
        }

        IQueryable<int> roleQuery;
        if (value.StartsWith(RolePrefix, StringComparison.Ordinal))
        {
            var roleCode = value[RolePrefix.Length..];
            if (roleCode.Length == 0)
            {
                return NotFound($"审批角色标识“{value}”不是有效的 role:<code> 格式");
            }

            roleQuery = db.Roles.AsNoTracking()
                .Where(x => x.IsActive && x.Code == roleCode)
                .Select(x => x.Id);
        }
        else
        {
            if (value.Contains(':'))
            {
                return NotFound($"审批角色标识“{value}”使用了不支持的结构化格式");
            }

            roleQuery = db.Roles.AsNoTracking()
                .Where(x => x.IsActive && (x.Code == value || x.Name == value))
                .Select(x => x.Id);
        }

        var roleIds = await roleQuery
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return NotFound($"审批角色标识“{value}”未匹配启用角色");
        }

        if (roleIds.Count > 1)
        {
            return Ambiguous([], $"审批角色标识“{value}”同时匹配多个启用角色");
        }

        var roleId = roleIds[0];
        var userIds = await db.UserRoles.AsNoTracking()
            .Where(x => x.RoleId == roleId && x.User.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        return Unique(userIds, userIds.Count == 0
            ? $"审批角色标识“{value}”唯一匹配角色，但该角色没有启用用户"
            : $"审批角色标识“{value}”唯一匹配角色及其启用用户");
    }

    private static bool TryParsePositiveId(string value, out int id)
    {
        id = 0;
        return value.Length > 0 &&
               value.All(char.IsAsciiDigit) &&
               int.TryParse(value, out id) &&
               id > 0;
    }

    private static ApproverIdentityResolution NotFound(string diagnostic)
        => new(ApproverIdentityResolutionStatus.NotFound, [], diagnostic);

    private static ApproverIdentityResolution Unique(IReadOnlyList<int> userIds, string diagnostic)
        => new(ApproverIdentityResolutionStatus.Unique, userIds, diagnostic);

    private static ApproverIdentityResolution Ambiguous(IReadOnlyList<int> userIds, string diagnostic)
        => new(ApproverIdentityResolutionStatus.Ambiguous, userIds, diagnostic);
}
