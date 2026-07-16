using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

public class OrganizationApprovalResolverTests : MySqlFixtureBase
{
    [Fact]
    public async Task Resolves_explicit_levels_with_division_without_department_names()
    {
        _db.OrganizationLevels.AddRange(
            new OrganizationLevel { Id = 791, Code = "company", Name = "公司", Sort = 10 },
            new OrganizationLevel { Id = 792, Code = "division", Name = "事业部", Sort = 20 },
            new OrganizationLevel { Id = 793, Code = "department", Name = "部门", Sort = 30 },
            new OrganizationLevel { Id = 794, Code = "section", Name = "课别", Sort = 40 });
        _db.Departments.AddRange(
            new Department { Id = 801, Code = "group", Name = "某集团", OrganizationLevelId = 791 },
            new Department { Id = 802, ParentId = 801, Code = "division", Name = "某事业群", OrganizationLevelId = 792, ManagerId = 815 },
            new Department { Id = 803, ParentId = 802, Code = "mechanical", Name = "机械工程部", OrganizationLevelId = 793, ManagerId = 811 },
            new Department { Id = 804, ParentId = 803, Code = "design", Name = "设计课", OrganizationLevelId = 794, ManagerId = 812 });
        _db.Users.AddRange(
            User(811, "部门负责人", 803),
            User(812, "课级负责人", 804),
            User(813, "普通课员", 804),
            User(814, "部门普通员工", 803),
            User(815, "事业部负责人", 802));
        await _db.SaveChangesAsync();

        var employee = await OrganizationApprovalResolver.ResolvePlanAsync(_db, 813);
        var sectionManager = await OrganizationApprovalResolver.ResolvePlanAsync(_db, 812);
        var departmentEmployee = await OrganizationApprovalResolver.ResolvePlanAsync(_db, 814);
        var departmentManager = await OrganizationApprovalResolver.ResolvePlanAsync(_db, 811);

        employee.Should().Be(new OrganizationApprovalPlan(813, 812, 811, true, true));
        sectionManager.Should().Be(new OrganizationApprovalPlan(812, 812, 811, false, true));
        departmentEmployee.Should().Be(new OrganizationApprovalPlan(814, null, 811, false, true));
        departmentManager.Should().Be(new OrganizationApprovalPlan(811, null, 811, false, false));

        (await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(_db, 813, "sectionManager"))
            .Should().Equal(812);
        (await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(_db, 813, "departmentManager"))
            .Should().Equal(811);
        (await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(_db, 813, "orgManager:division"))
            .Should().Equal(815);
        var division = await OrganizationApprovalResolver.ResolveTargetAsync(_db, 813, "division");
        division.RequiresApproval.Should().BeTrue();
        division.OrganizationId.Should().Be(802);
    }

    private static User User(int id, string name, int departmentId) => new()
    {
        Id = id,
        EmployeeNo = $"T{id}",
        Name = name,
        PasswordHash = "test",
        DepartmentId = departmentId,
        IsActive = true,
    };
}
