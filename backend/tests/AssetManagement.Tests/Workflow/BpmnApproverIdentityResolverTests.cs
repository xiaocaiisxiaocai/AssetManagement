using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

public class BpmnApproverIdentityResolverTests : MySqlFixtureBase
{
    [Fact]
    public async Task Structured_user_identity_matches_only_active_user_id()
    {
        var selected = AddUser(101, "9001", "选定用户");
        AddUser(102, "101", "工号碰撞用户");
        AddUser(103, "9003", "停用用户", isActive: false);
        await _db.SaveChangesAsync();

        var selectedResult = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "user:101");
        var inactiveResult = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "user:103");
        var invalidResult = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "user:0");

        selectedResult.Status.Should().Be(ApproverIdentityResolutionStatus.Unique);
        selectedResult.UserIds.Should().Equal(selected.Id);
        inactiveResult.Status.Should().Be(ApproverIdentityResolutionStatus.NotFound);
        invalidResult.Status.Should().Be(ApproverIdentityResolutionStatus.NotFound);
    }

    [Fact]
    public async Task Legacy_user_identity_fails_closed_when_fields_match_different_users()
    {
        AddUser(201, "9201", "用户甲");
        AddUser(202, "201", "用户乙");
        AddUser(203, "9203", "同名用户");
        AddUser(204, "9204", "同名用户");
        AddUser(205, "205", "字段重叠用户");
        await _db.SaveChangesAsync();

        var numericCollision = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "201");
        var duplicateName = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "同名用户");
        var sameUserOverlap = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "205");
        var missing = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, "不存在");

        numericCollision.Status.Should().Be(ApproverIdentityResolutionStatus.Ambiguous);
        numericCollision.UserIds.Should().Equal(201, 202);
        duplicateName.Status.Should().Be(ApproverIdentityResolutionStatus.Ambiguous);
        duplicateName.UserIds.Should().Equal(203, 204);
        sameUserOverlap.Status.Should().Be(ApproverIdentityResolutionStatus.Unique);
        sameUserOverlap.UserIds.Should().Equal(205);
        missing.Status.Should().Be(ApproverIdentityResolutionStatus.NotFound);
    }

    [Fact]
    public async Task Group_identity_requires_one_active_role_and_returns_only_active_users()
    {
        var supervisor = AddRole(301, "supervisor", "部门主管");
        AddRole(302, "部门主管", "另一角色");
        var disabledRole = AddRole(303, "disabled", "停用角色", isActive: false);
        var activeUser = AddUser(301, "9301", "启用主管");
        var inactiveUser = AddUser(302, "9302", "停用主管", isActive: false);
        var disabledRoleUser = AddUser(303, "9303", "停用角色成员");
        _db.UserRoles.AddRange(
            new UserRole { UserId = activeUser.Id, RoleId = supervisor.Id },
            new UserRole { UserId = inactiveUser.Id, RoleId = supervisor.Id },
            new UserRole { UserId = disabledRoleUser.Id, RoleId = disabledRole.Id });
        await _db.SaveChangesAsync();

        var structured = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(_db, "role:supervisor");
        var ambiguousLegacy = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(_db, "部门主管");
        var inactiveRole = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(_db, "role:disabled");

        structured.Status.Should().Be(ApproverIdentityResolutionStatus.Unique);
        structured.UserIds.Should().Equal(activeUser.Id);
        ambiguousLegacy.Status.Should().Be(ApproverIdentityResolutionStatus.Ambiguous);
        ambiguousLegacy.UserIds.Should().BeEmpty();
        inactiveRole.Status.Should().Be(ApproverIdentityResolutionStatus.NotFound);
    }

    private User AddUser(int id, string employeeNo, string name, bool isActive = true)
    {
        var user = new User
        {
            Id = id,
            EmployeeNo = employeeNo,
            Name = name,
            PasswordHash = "test",
            IsActive = isActive,
        };
        _db.Users.Add(user);
        return user;
    }

    private Role AddRole(int id, string code, string name, bool isActive = true)
    {
        var role = new Role { Id = id, Code = code, Name = name, IsActive = isActive };
        _db.Roles.Add(role);
        return role;
    }
}
