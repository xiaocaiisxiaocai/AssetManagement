using AssetManagement.Infrastructure.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

public class OrganizationApprovalPlanTests
{
    [Fact]
    public void Section_employee_requires_section_then_department_approval()
    {
        var plan = OrganizationApprovalPlan.Create(100, true, 200, 300);

        plan.SectionManagerId.Should().Be(200);
        plan.DepartmentManagerId.Should().Be(300);
        plan.RequiresSectionApproval.Should().BeTrue();
        plan.RequiresDepartmentApproval.Should().BeTrue();
    }

    [Fact]
    public void Section_manager_skips_self_and_requires_department_approval()
    {
        var plan = OrganizationApprovalPlan.Create(200, true, 200, 300);

        plan.RequiresSectionApproval.Should().BeFalse();
        plan.RequiresDepartmentApproval.Should().BeTrue();
    }

    [Fact]
    public void Department_employee_only_requires_department_approval()
    {
        var plan = OrganizationApprovalPlan.Create(100, false, 300, null);

        plan.SectionManagerId.Should().BeNull();
        plan.DepartmentManagerId.Should().Be(300);
        plan.RequiresSectionApproval.Should().BeFalse();
        plan.RequiresDepartmentApproval.Should().BeTrue();
    }

    [Fact]
    public void Department_manager_skips_all_approval_nodes()
    {
        var plan = OrganizationApprovalPlan.Create(300, false, 300, null);

        plan.RequiresSectionApproval.Should().BeFalse();
        plan.RequiresDepartmentApproval.Should().BeFalse();
    }

    [Fact]
    public void Missing_required_manager_stays_required_so_start_cannot_silently_pass()
    {
        var plan = OrganizationApprovalPlan.Create(100, true, null, null);

        plan.RequiresSectionApproval.Should().BeTrue();
        plan.RequiresDepartmentApproval.Should().BeTrue();
    }
}
