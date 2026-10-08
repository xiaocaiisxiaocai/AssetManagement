using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

/// <summary>
/// 解析当前用户任务仍可审批的启用用户。歧义标识跳过，不猜其中一人。
/// 资产转让的 Task_receiver 按接收人部门解析，与催办提醒一致。
/// </summary>
public static class UserTaskApproverResolver
{
    public static async Task<List<int>> ResolveAsync(
        AppDbContext db,
        BpmnNode node,
        int applicantId,
        int? transfereeId = null,
        string? bizType = null)
    {
        var result = new List<int>();
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
                var resolution = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(db, group);
                if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
                {
                    continue;
                }

                foreach (var uid in resolution.UserIds)
                {
                    Add(result, uid);
                }
            }
        }

        return result;
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

        var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == departmentId);
        if (department?.ManagerId is int managerId && managerId != applicantId &&
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
        {
            Add(result, managerId);
        }

        var supervisorIds = await db.Users
            .AsNoTracking()
            .Where(u => u.Id != applicantId && u.IsActive && u.DepartmentId == departmentId &&
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
