using AssetManagement.Application.Common;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

public sealed record OrganizationApprovalPlan(
    int ApplicantId,
    int? SectionManagerId,
    int? DepartmentManagerId,
    bool RequiresSectionApproval,
    bool RequiresDepartmentApproval)
{
    public static OrganizationApprovalPlan Create(
        int applicantId,
        bool isSectionLevel,
        int? currentManagerId,
        int? parentManagerId)
    {
        var sectionManagerId = isSectionLevel ? currentManagerId : null;
        var departmentManagerId = isSectionLevel ? parentManagerId : currentManagerId;

        return new OrganizationApprovalPlan(
            applicantId,
            sectionManagerId,
            departmentManagerId,
            isSectionLevel && sectionManagerId != applicantId,
            departmentManagerId != applicantId);
    }
}

public static class OrganizationApprovalResolver
{
    public const string SectionManagerAssignee = "sectionManager";
    public const string DepartmentManagerAssignee = "departmentManager";

    public static bool IsOrganizationAssignee(string? assignee)
        => assignee is SectionManagerAssignee or DepartmentManagerAssignee;

    public static bool IsUsedBy(BpmnProcess process)
        => process.Nodes.Any(node =>
               IsOrganizationAssignee(node.Properties.GetValueOrDefault("assignee"))) ||
           process.Flows.Any(flow =>
               flow.ConditionExpression?.Contains("requiresSectionApproval", StringComparison.Ordinal) == true ||
               flow.ConditionExpression?.Contains("requiresDepartmentApproval", StringComparison.Ordinal) == true);

    public static async Task<OrganizationApprovalPlan> ResolvePlanAsync(
        AppDbContext db,
        int applicantId,
        CancellationToken cancellationToken = default)
    {
        var applicant = await db.Users.AsNoTracking()
            .Where(x => x.Id == applicantId && x.IsActive)
            .Select(x => new { x.Id, x.DepartmentId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BizException(4041, "申请人不存在或已停用");
        if (applicant.DepartmentId is null)
            throw new BizException(4051, "申请人未配置所属组织，无法解析审批链");

        var current = await db.Departments.AsNoTracking()
            .Where(x => x.Id == applicant.DepartmentId.Value && x.IsActive)
            .Select(x => new { x.ParentId, x.ManagerId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BizException(4051, "申请人所属组织不存在或已停用");

        int? parentManagerId = null;
        var isSectionLevel = false;
        if (current.ParentId is int parentId)
        {
            var parent = await db.Departments.AsNoTracking()
                .Where(x => x.Id == parentId && x.IsActive)
                .Select(x => new { x.ParentId, x.ManagerId })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new BizException(4051, "申请人所属组织的上级组织不存在或已停用");
            isSectionLevel = parent.ParentId.HasValue;
            parentManagerId = parent.ManagerId;
        }

        return OrganizationApprovalPlan.Create(
            applicant.Id,
            isSectionLevel,
            current.ManagerId,
            parentManagerId);
    }

    public static async Task<List<int>> ResolveApproverUserIdsAsync(
        AppDbContext db,
        int applicantId,
        string assignee,
        CancellationToken cancellationToken = default)
    {
        var plan = await ResolvePlanAsync(db, applicantId, cancellationToken);
        var managerId = assignee switch
        {
            SectionManagerAssignee => plan.SectionManagerId,
            DepartmentManagerAssignee => plan.DepartmentManagerId,
            _ => null
        };
        if (managerId is null) return [];

        return await db.Users.AsNoTracking()
            .Where(x => x.Id == managerId.Value && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
