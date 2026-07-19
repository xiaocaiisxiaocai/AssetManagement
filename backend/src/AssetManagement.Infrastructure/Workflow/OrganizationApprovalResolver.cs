using System.Text.RegularExpressions;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

public sealed record OrganizationApprovalTarget(
    string LevelCode,
    string? LevelName,
    int? OrganizationId,
    int? ManagerId,
    bool Exists,
    bool RequiresApproval);

// 保留旧计划类型，供旧流程兼容和渐进迁移使用。
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

public static partial class OrganizationApprovalResolver
{
    public const string OrganizationManagerPrefix = "orgManager:";
    public const string SectionManagerAssignee = "sectionManager";
    public const string DepartmentManagerAssignee = "departmentManager";

    public static string OrganizationManagerAssignee(string levelCode)
        => $"{OrganizationManagerPrefix}{NormalizeLevelCode(levelCode)}";

    public static string ApprovalConditionKey(string levelCode)
        => $"requiresApproval_{NormalizeLevelCode(levelCode)}";

    public static string? GetOrganizationLevelCode(string? assignee)
    {
        if (assignee == SectionManagerAssignee) return "section";
        if (assignee == DepartmentManagerAssignee) return "department";
        if (assignee?.StartsWith(OrganizationManagerPrefix, StringComparison.Ordinal) != true) return null;
        var code = assignee[OrganizationManagerPrefix.Length..];
        return IsValidLevelCode(code) ? code : null;
    }

    public static bool IsOrganizationAssignee(string? assignee)
        => GetOrganizationLevelCode(assignee) is not null;

    public static IReadOnlyCollection<string> GetRequestedLevelCodes(BpmnProcess process)
    {
        var result = process.Nodes
            .Select(node => GetOrganizationLevelCode(node.Properties.GetValueOrDefault("assignee")))
            .Where(code => code is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var flow in process.Flows)
        {
            var expression = flow.ConditionExpression ?? "";
            foreach (Match match in DynamicConditionRegex().Matches(expression))
                result.Add(match.Groups[1].Value);
            if (expression.Contains("requiresSectionApproval", StringComparison.Ordinal)) result.Add("section");
            if (expression.Contains("requiresDepartmentApproval", StringComparison.Ordinal)) result.Add("department");
        }
        return result;
    }

    public static bool IsUsedBy(BpmnProcess process)
        => GetRequestedLevelCodes(process).Count > 0;

    public static async Task<OrganizationApprovalTarget> ResolveTargetAsync(
        AppDbContext db,
        int applicantId,
        string levelCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeLevelCode(levelCode);
        var applicant = await db.Users.AsNoTracking()
            .Where(x => x.Id == applicantId && x.IsActive)
            .Select(x => new { x.Id, x.DepartmentId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BizException(4041, "申请人不存在或已停用");
        if (!applicant.DepartmentId.HasValue)
            throw new BizException(4051, "申请人未配置所属组织，无法解析审批链");

        var level = await db.OrganizationLevels.AsNoTracking()
            .Where(x => x.Code == normalizedCode && x.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BizException(4051, $"流程引用的组织层级“{normalizedCode}”不存在或已停用");
        var organizations = await db.Departments.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.ParentId, x.ManagerId, x.OrganizationLevelId })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var visited = new HashSet<int>();
        var cursorId = applicant.DepartmentId.Value;
        while (organizations.TryGetValue(cursorId, out var organization))
        {
            if (!visited.Add(cursorId))
                throw new BizException(4051, "申请人所属组织存在循环上级关系，无法解析审批链");
            if (organization.OrganizationLevelId == level.Id)
            {
                return new OrganizationApprovalTarget(
                    level.Code,
                    level.Name,
                    organization.Id,
                    organization.ManagerId,
                    true,
                    organization.ManagerId != applicant.Id);
            }
            if (!organization.ParentId.HasValue) break;
            cursorId = organization.ParentId.Value;
        }

        return new OrganizationApprovalTarget(level.Code, level.Name, null, null, false, false);
    }

    public static async Task<OrganizationApprovalPlan> ResolvePlanAsync(
        AppDbContext db,
        int applicantId,
        CancellationToken cancellationToken = default)
    {
        var section = await ResolveTargetAsync(db, applicantId, "section", cancellationToken);
        var department = await ResolveTargetAsync(db, applicantId, "department", cancellationToken);
        return new OrganizationApprovalPlan(
            applicantId,
            section.ManagerId,
            department.ManagerId,
            section.RequiresApproval,
            department.RequiresApproval);
    }

    public static async Task<List<int>> ResolveApproverUserIdsAsync(
        AppDbContext db,
        int applicantId,
        string assignee,
        CancellationToken cancellationToken = default)
    {
        var levelCode = GetOrganizationLevelCode(assignee);
        if (levelCode is null) return [];
        var target = await ResolveTargetAsync(db, applicantId, levelCode, cancellationToken);
        if (!target.ManagerId.HasValue) return [];
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == target.ManagerId.Value && x.Id != applicantId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeLevelCode(string levelCode)
    {
        var normalized = levelCode.Trim();
        if (!IsValidLevelCode(normalized))
            throw new BizException(4051, $"组织层级代码“{levelCode}”格式不正确");
        return normalized;
    }

    private static bool IsValidLevelCode(string code)
        => LevelCodeRegex().IsMatch(code);

    [GeneratedRegex("^[a-z][a-z0-9_]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex LevelCodeRegex();

    [GeneratedRegex(@"\$\{requiresApproval_([a-z][a-z0-9_]{0,49})\}", RegexOptions.CultureInvariant)]
    private static partial Regex DynamicConditionRegex();
}
