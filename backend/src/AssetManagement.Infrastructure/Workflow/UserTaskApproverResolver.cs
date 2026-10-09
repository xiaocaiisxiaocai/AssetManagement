using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

/// <summary>
/// 解析当前用户任务仍可审批的启用用户。歧义标识跳过，不猜其中一人。
/// 资产归还，以及转让流程的 Task_adminRole，按资产所属部门的主管解析并排除申请人。
/// 资产转让的 Task_receiver 按接收人部门解析。
/// </summary>
public static class UserTaskApproverResolver
{
    public static bool UsesAssetDepartmentScope(string? bizType, BpmnNode node)
    {
        if (!IsDepartmentSupervisorNode(node))
        {
            return false;
        }

        return bizType == "return" || (bizType == "transfer" && node.Id == "Task_adminRole");
    }

    public static bool NeedsAssetDepartment(string? bizType, BpmnNode node)
        => bizType == "return" || (bizType == "transfer" && node.Id == "Task_adminRole");

    public static async Task<string?> AmbiguousIdentityDiagnosticAsync(AppDbContext db, BpmnNode node)
    {
        var assignee = node.Properties.GetValueOrDefault("assignee");
        if (!string.IsNullOrEmpty(assignee)
            && assignee is not ("deptManager" or "supervisor")
            && !OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
        {
            var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(db, assignee);
            if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
            {
                return resolution.Diagnostic;
            }
        }

        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(db, part);
                if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
                {
                    return resolution.Diagnostic;
                }
            }
        }

        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");
        if (!string.IsNullOrEmpty(candidateGroups))
        {
            foreach (var group in candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(db, group);
                if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
                {
                    return resolution.Diagnostic;
                }
            }
        }

        return null;
    }

    public static async Task<List<int>> ResolveAsync(
        AppDbContext db,
        BpmnNode node,
        int applicantId,
        int? transfereeId = null,
        string? bizType = null,
        int? assetDepartmentId = null)
    {
        var result = new List<int>();
        if (UsesAssetDepartmentScope(bizType, node))
        {
            if (assetDepartmentId is int scopedDepartmentId)
            {
                await AddDepartmentSupervisorsAsync(db, scopedDepartmentId, applicantId, result);
            }

            return result;
        }

        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
            {
                foreach (var uid in await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(
                             db, applicantId, assignee))
                {
                    Add(result, uid);
                }
            }
            else if (assignee == "deptManager")
            {
                await AddDepartmentManagersAsync(db, node, applicantId, transfereeId, bizType, result);
            }
            else if (assignee == "supervisor")
            {
                foreach (var supervisorId in await ResolveSupervisorApproverUserIdsAsync(db, applicantId))
                {
                    Add(result, supervisorId);
                }
            }
            else
            {
                await AddResolvedUsersAsync(db, assignee, result);
            }
        }

        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await AddResolvedUsersAsync(db, part, result);
            }
        }

        if (!string.IsNullOrEmpty(candidateGroups))
        {
            foreach (var group in candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await AddCandidateGroupAsync(db, node, group, bizType, applicantId, assetDepartmentId, result);
            }
        }

        if (bizType == "return" || (bizType == "transfer" && node.Id == "Task_adminRole"))
        {
            result.RemoveAll(id => id == applicantId);
        }

        return result;
    }

    private static bool IsDepartmentSupervisorNode(BpmnNode node)
    {
        var assignee = node.Properties.GetValueOrDefault("assignee");
        if (assignee == "deptManager")
        {
            return true;
        }

        return string.IsNullOrEmpty(assignee)
               && IsOnlySupervisorRoleGroup(node.Properties.GetValueOrDefault("candidateGroups"));
    }

    private static bool IsOnlySupervisorRoleGroup(string? groups)
    {
        if (string.IsNullOrWhiteSpace(groups))
        {
            return false;
        }

        var parts = groups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 && parts.All(IsSupervisorRoleToken);
    }

    private static bool ShouldScopeSupervisorGroup(string? bizType, string nodeId, string group)
    {
        if (!IsSupervisorRoleToken(group))
        {
            return false;
        }

        return bizType == "return" || (bizType == "transfer" && nodeId == "Task_adminRole");
    }

    private static bool IsSupervisorRoleToken(string group)
        => group.Equals("role:supervisor", StringComparison.OrdinalIgnoreCase)
           || group.Equals("supervisor", StringComparison.OrdinalIgnoreCase);

    private static async Task AddCandidateGroupAsync(
        AppDbContext db,
        BpmnNode node,
        string group,
        string? bizType,
        int applicantId,
        int? assetDepartmentId,
        List<int> result)
    {
        if (ShouldScopeSupervisorGroup(bizType, node.Id, group))
        {
            if (assetDepartmentId is int departmentId)
            {
                await AddDepartmentSupervisorsAsync(db, departmentId, applicantId, result);
            }

            return;
        }

        var resolution = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(db, group);
        if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
        {
            return;
        }

        foreach (var uid in resolution.UserIds)
        {
            Add(result, uid);
        }
    }

    private static async Task AddDepartmentManagersAsync(
        AppDbContext db,
        BpmnNode node,
        int applicantId,
        int? transfereeId,
        string? bizType,
        List<int> result)
    {
        var targetUserId = bizType == "transfer" && node.Id == "Task_receiver" && transfereeId.HasValue
            ? transfereeId.Value
            : applicantId;
        var targetUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetUserId);
        if (targetUser?.DepartmentId is not int departmentId)
        {
            return;
        }

        await AddDepartmentSupervisorsAsync(db, departmentId, applicantId, result);
    }

    private static async Task AddDepartmentSupervisorsAsync(
        AppDbContext db,
        int departmentId,
        int excludeUserId,
        List<int> result)
    {
        var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == departmentId);
        if (department?.ManagerId is int managerId && managerId != excludeUserId &&
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
        {
            Add(result, managerId);
        }

        var supervisorIds = await db.Users
            .AsNoTracking()
            .Where(u => u.Id != excludeUserId && u.IsActive && u.DepartmentId == departmentId &&
                        u.UserRoles.Any(ur => ur.Role != null && ur.Role.IsActive && ur.Role.Code == "supervisor"))
            .Select(u => u.Id)
            .ToListAsync();
        foreach (var supervisorId in supervisorIds)
        {
            Add(result, supervisorId);
        }
    }

    private static async Task AddResolvedUsersAsync(AppDbContext db, string identity, List<int> result)
    {
        var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(db, identity);
        if (resolution.Status != ApproverIdentityResolutionStatus.Unique)
        {
            return;
        }

        Add(result, resolution.UserIds[0]);
    }

    private static async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(AppDbContext db, int applicantId)
    {
        var result = new List<int>();
        var applicant = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicantId);
        if (applicant?.DepartmentId is not null)
        {
            var department = await db.Departments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
            if (department?.ManagerId is int managerId && managerId != applicantId &&
                await db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
            {
                result.Add(managerId);
            }
        }

        if (result.Count == 0 && applicant?.SupervisorId is int supervisorId && supervisorId != applicantId &&
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == supervisorId && x.IsActive))
        {
            result.Add(supervisorId);
        }

        return result;
    }

    private static void Add(List<int> result, int userId)
    {
        if (!result.Contains(userId))
        {
            result.Add(userId);
        }
    }
}
