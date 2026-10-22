using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetManagement.Tests.TestMaterials;

public class TestMaterialAccessScopeApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public TestMaterialAccessScopeApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Employee_only_sees_materials_related_by_project_custody_or_flow()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var employeeRole = await db.Roles.SingleAsync(x => x.Code == "employee");
        var departmentA = new Department { Code = $"EA-{suffix}", Name = $"员工甲部门-{suffix}", IsActive = true };
        var departmentB = new Department { Code = $"EB-{suffix}", Name = $"员工乙部门-{suffix}", IsActive = true };
        db.Departments.AddRange(departmentA, departmentB);
        await db.SaveChangesAsync();
        var employeeA = NewUser($"EA-{suffix}", "员工甲", departmentA.Id);
        var employeeB = NewUser($"EB-{suffix}", "员工乙", departmentB.Id);
        db.Users.AddRange(employeeA, employeeB);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(
            new UserRole { UserId = employeeA.Id, RoleId = employeeRole.Id },
            new UserRole { UserId = employeeB.Id, RoleId = employeeRole.Id });
        var ownedProject = NewProject($"员工甲负责项目-{suffix}", employeeA.Id);
        var otherProject = NewProject($"员工乙负责项目-{suffix}", employeeB.Id);
        db.TestProjects.AddRange(ownedProject, otherProject);
        await db.SaveChangesAsync();
        var ownerRelated = NewMaterial($"OWN-{suffix}", "负责人相关料件", ownedProject.Id, departmentB.Id, employeeB.Id);
        var custodyRelated = NewMaterial($"KEEP-{suffix}", "保管相关料件", otherProject.Id, departmentA.Id, employeeA.Id);
        var flowRelated = NewMaterial($"FLOW-{suffix}", "流转相关料件", otherProject.Id, departmentB.Id, employeeB.Id);
        var unrelated = NewMaterial($"NONE-{suffix}", "无关料件", otherProject.Id, departmentB.Id, employeeB.Id);
        db.TestMaterials.AddRange(ownerRelated, custodyRelated, flowRelated, unrelated);
        await db.SaveChangesAsync();
        db.MaterialFlows.Add(new MaterialFlow
        {
            FlowNo = $"MF-{suffix}", MaterialId = flowRelated.Id,
            MaterialNo = flowRelated.MaterialNo, MaterialName = flowRelated.Name,
            ApplicantId = employeeA.Id, Applicant = employeeA.Name,
            TransfereeId = employeeB.Id, Transferee = employeeB.Name,
            Status = "completed", ApplyTime = DateTime.UtcNow.AddMinutes(-1),
            Deadline = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        await Login(employeeA.EmployeeNo);
        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestMaterialDto>>>(
            "/api/test-materials?deleteStatus=all&page=1&pageSize=100");

        list!.Data!.Items.Select(x => x.Id).Should().Contain(new[]
        {
            ownerRelated.Id, custodyRelated.Id, flowRelated.Id
        }).And.NotContain(unrelated.Id);
        (await _client.GetAsync($"/api/test-materials/{unrelated.Id}/detail"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Supervisor_can_manage_own_department_project_but_not_another_department_project()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supervisorRole = await db.Roles.SingleAsync(x => x.Code == "supervisor");
        var employeeRole = await db.Roles.SingleAsync(x => x.Code == "employee");
        var ownDepartment = new Department { Code = $"SA-{suffix}", Name = $"主管部门-{suffix}", IsActive = true };
        var otherDepartment = new Department { Code = $"SB-{suffix}", Name = $"其他部门-{suffix}", IsActive = true };
        db.Departments.AddRange(ownDepartment, otherDepartment);
        await db.SaveChangesAsync();
        var supervisor = NewUser($"SUP-{suffix}", "部门主管", ownDepartment.Id);
        var ownOwner = NewUser($"OWN-{suffix}", "本部门负责人", ownDepartment.Id);
        var otherOwner = NewUser($"OTH-{suffix}", "其他部门负责人", otherDepartment.Id);
        db.Users.AddRange(supervisor, ownOwner, otherOwner);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(
            new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id },
            new UserRole { UserId = ownOwner.Id, RoleId = employeeRole.Id },
            new UserRole { UserId = otherOwner.Id, RoleId = employeeRole.Id });
        var ownProject = NewProject($"本部门项目-{suffix}", ownOwner.Id);
        var otherProject = NewProject($"其他部门项目-{suffix}", otherOwner.Id);
        otherProject.ProjectTypeCode = $"scope-{suffix}";
        db.TestProjects.AddRange(ownProject, otherProject);
        await db.SaveChangesAsync();

        await Login(supervisor.EmployeeNo);
        var ownUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{ownProject.Id}/progress",
            new { ProgressCode = "testing", TestStatus = "本部门可维护", RowVersion = ownProject.RowVersion });
        ownUpdate.EnsureSuccessStatusCode();

        var crossUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{otherProject.Id}", ProjectRequest(otherProject, "跨部门修改"));
        var crossProgress = await _client.PutAsJsonAsync(
            $"/api/test-projects/{otherProject.Id}/progress",
            new { ProgressCode = "testing", TestStatus = "跨部门进展", RowVersion = otherProject.RowVersion });
        var visible = await _client.GetFromJsonAsync<ApiResult<List<TestProjectDto>>>(
            "/api/test-projects?deleteStatus=all");
        var stats = await _client.GetFromJsonAsync<ApiResult<TestProjectStatsDto>>("/api/test-projects/stats");
        var workspace = await _client.GetAsync($"/api/test-projects/{otherProject.Id}/workspace");
        var followups = await _client.GetAsync($"/api/test-projects/{otherProject.Id}/followups");
        var crossStage = await _client.PostAsJsonAsync(
            $"/api/test-projects/{otherProject.Id}/workspace/stages",
            new SaveProjectTestStageRequest { Name = "跨部门阶段", Sequence = 1 });
        var crossDelete = await _client.DeleteAsync($"/api/test-projects/{otherProject.Id}");

        crossUpdate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        crossProgress.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        visible!.Data!.Should().Contain(x => x.Id == ownProject.Id);
        visible.Data.Should().NotContain(x => x.Id == otherProject.Id);
        stats!.Data!.TypeDist.Should().NotContain(x => x.Label == $"scope-{suffix}");
        workspace.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        followups.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        crossStage.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        crossDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Custom_role_with_project_edit_is_limited_to_its_department_tree()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var editPermission = await db.Permissions.SingleAsync(x => x.Code == "project:edit");
        var ownDepartment = new Department { Code = $"CA-{suffix}", Name = $"自定义部门-{suffix}", IsActive = true };
        var otherDepartment = new Department { Code = $"CB-{suffix}", Name = $"自定义外部门-{suffix}", IsActive = true };
        var role = new Role { Code = $"cust-{suffix}", Name = $"自定义编辑-{suffix}", IsActive = true };
        db.Departments.AddRange(ownDepartment, otherDepartment);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = editPermission.Id });
        var editor = NewUser($"CE-{suffix}", "自定义编辑人", ownDepartment.Id);
        var colleague = NewUser($"CC-{suffix}", "同部门同事", ownDepartment.Id);
        var outsider = NewUser($"CO-{suffix}", "外部门负责人", otherDepartment.Id);
        db.Users.AddRange(editor, colleague, outsider);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = editor.Id, RoleId = role.Id });
        var ownProject = NewProject($"自定义本部门-{suffix}", colleague.Id);
        var otherProject = NewProject($"自定义外部门-{suffix}", outsider.Id);
        db.TestProjects.AddRange(ownProject, otherProject);
        await db.SaveChangesAsync();

        await Login(editor.EmployeeNo);
        var ownUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{ownProject.Id}", ProjectRequest(ownProject, $"本部门可改-{suffix}"));
        var crossUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{otherProject.Id}", ProjectRequest(otherProject, $"外部门不可改-{suffix}"));

        ownUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        crossUpdate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Project_row_actions_follow_department_scope_and_owner_progress_rights()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supervisorRole = await db.Roles.SingleAsync(x => x.Code == "supervisor");
        var employeeRole = await db.Roles.SingleAsync(x => x.Code == "employee");
        var ownDepartment = new Department { Code = $"RA-{suffix}", Name = $"行权限部门-{suffix}", IsActive = true };
        var otherDepartment = new Department { Code = $"RB-{suffix}", Name = $"行权限外部门-{suffix}", IsActive = true };
        db.Departments.AddRange(ownDepartment, otherDepartment);
        await db.SaveChangesAsync();
        var supervisor = NewUser($"RS-{suffix}", "行权限主管", ownDepartment.Id);
        var employee = NewUser($"RE-{suffix}", "行权限员工", ownDepartment.Id);
        var outsider = NewUser($"RO-{suffix}", "行权限外人", otherDepartment.Id);
        db.Users.AddRange(supervisor, employee, outsider);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(
            new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id },
            new UserRole { UserId = employee.Id, RoleId = employeeRole.Id });
        var ownProject = NewProject($"行权限本部门-{suffix}", employee.Id);
        var otherProject = NewProject($"行权限外部门-{suffix}", outsider.Id);
        db.TestProjects.AddRange(ownProject, otherProject);
        await db.SaveChangesAsync();

        await Login(supervisor.EmployeeNo);
        var supervisorList = await _client.GetFromJsonAsync<ApiResult<List<TestProjectDto>>>(
            "/api/test-projects?deleteStatus=all");
        var supervisorItems = supervisorList!.Data!;
        supervisorItems.Should().Contain(x => x.Id == ownProject.Id);
        supervisorItems.Should().NotContain(x => x.Id == otherProject.Id);
        supervisorItems.Single(x => x.Id == ownProject.Id).CanEdit.Should().BeTrue();
        supervisorItems.Single(x => x.Id == ownProject.Id).CanUpdateProgress.Should().BeTrue();

        await Login(employee.EmployeeNo);
        var employeeList = await _client.GetFromJsonAsync<ApiResult<List<TestProjectDto>>>(
            "/api/test-projects?deleteStatus=all");
        employeeList!.Data!.Should().NotContain(x => x.Id == otherProject.Id);
        var owned = employeeList.Data!.Single(x => x.Id == ownProject.Id);
        owned.CanEdit.Should().BeFalse();
        owned.CanUpdateProgress.Should().BeTrue();
    }

    [Fact]
    public async Task Non_supervisor_can_only_create_a_project_for_self_and_supervisor_stays_in_department()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supervisorRole = await db.Roles.SingleAsync(x => x.Code == "supervisor");
        var employeeRole = await db.Roles.SingleAsync(x => x.Code == "employee");
        var ownDepartment = new Department { Code = $"OA-{suffix}", Name = $"负责人部门-{suffix}", IsActive = true };
        var otherDepartment = new Department { Code = $"OB-{suffix}", Name = $"负责人外部门-{suffix}", IsActive = true };
        db.Departments.AddRange(ownDepartment, otherDepartment);
        await db.SaveChangesAsync();
        var employee = NewUser($"OE-{suffix}", "创建员工", ownDepartment.Id);
        var colleague = NewUser($"OC-{suffix}", "同部门同事", ownDepartment.Id);
        var outsider = NewUser($"OO-{suffix}", "外部门人员", otherDepartment.Id);
        var supervisor = NewUser($"OS-{suffix}", "创建主管", ownDepartment.Id);
        db.Users.AddRange(employee, colleague, outsider, supervisor);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(
            new UserRole { UserId = employee.Id, RoleId = employeeRole.Id },
            new UserRole { UserId = colleague.Id, RoleId = employeeRole.Id },
            new UserRole { UserId = supervisor.Id, RoleId = supervisorRole.Id });
        await db.SaveChangesAsync();

        await Login(employee.EmployeeNo);
        var rejected = await _client.PostAsJsonAsync(
            "/api/test-projects", CreateRequest($"员工指定他人-{suffix}", outsider.Id));
        rejected.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rejected.Content.ReadFromJsonAsync<ApiResult<object>>())!.Message
            .Should().Contain("只能把自己设为项目负责人");

        await Login(supervisor.EmployeeNo);
        var outsideOwner = await _client.PostAsJsonAsync(
            "/api/test-projects", CreateRequest($"主管指定外部门-{suffix}", outsider.Id));
        outsideOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsideOwner.Content.ReadFromJsonAsync<ApiResult<object>>())!.Message
            .Should().Contain("只能指定本部门及下级部门的人员为负责人");
        var insideOwner = await _client.PostAsJsonAsync(
            "/api/test-projects", CreateRequest($"主管指定本部门-{suffix}", colleague.Id));
        insideOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Project_owner_can_dispose_open_materials_without_material_permissions()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var employeeRole = await db.Roles.SingleAsync(x => x.Code == "employee");
        var department = new Department { Code = $"MD-{suffix}", Name = $"料件部门-{suffix}", IsActive = true };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        var owner = NewUser($"MO-{suffix}", "项目负责人", department.Id);
        var custodian = NewUser($"MC-{suffix}", "仅保管人", department.Id);
        db.Users.AddRange(owner, custodian);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(
            new UserRole { UserId = owner.Id, RoleId = employeeRole.Id },
            new UserRole { UserId = custodian.Id, RoleId = employeeRole.Id });
        var project = NewProject($"负责人料件-{suffix}", owner.Id);
        db.TestProjects.Add(project);
        await db.SaveChangesAsync();
        var deletable = NewMaterial($"TM-D-{suffix}", $"可删除-{suffix}", project.Id, department.Id, custodian.Id);
        var returnable = NewMaterial($"TM-R-{suffix}", $"可退回-{suffix}", project.Id, department.Id, custodian.Id);
        db.TestMaterials.AddRange(deletable, returnable);
        await db.SaveChangesAsync();

        await Login(custodian.EmployeeNo);
        (await _client.DeleteAsync($"/api/test-materials/{deletable.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await Login(owner.EmployeeNo);
        (await _client.DeleteAsync($"/api/test-materials/{deletable.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsync($"/api/test-materials/{deletable.Id}/restore", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.DeleteAsync($"/api/test-materials/{deletable.Id}/purge"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PostAsync($"/api/test-materials/{returnable.Id}/return", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static SaveTestProjectRequest CreateRequest(string name, int ownerId) => new()
    {
        Name = name,
        Code = $"P-{Guid.NewGuid():N}"[..24],
        ProjectTypeCode = "prototype",
        ProgressCode = "planning",
        OwnerId = ownerId,
        StartDate = BusinessClock.Today,
        PlannedFinishDate = BusinessClock.Today.AddDays(10),
        FollowUpIntervalDays = 14
    };

    private static User NewUser(string employeeNo, string name, int departmentId) => new()
    {
        EmployeeNo = employeeNo,
        Name = name,
        DepartmentId = departmentId,
        IsActive = true,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
    };

    private static TestProject NewProject(string name, int ownerId) => new()
    {
        Name = name,
        Code = $"P-{Guid.NewGuid():N}"[..24],
        ProjectTypeCode = "prototype",
        ProgressCode = "testing",
        OwnerId = ownerId,
        StartDate = BusinessClock.Today.AddDays(-2),
        PlannedFinishDate = BusinessClock.Today.AddDays(10),
        FollowUpIntervalDays = 14,
        CreatedAt = DateTime.UtcNow
    };

    private static TestMaterial NewMaterial(
        string materialNo, string name, int projectId, int departmentId, int custodianId) => new()
    {
        MaterialNo = materialNo,
        Name = name,
        ProjectId = projectId,
        DepartmentId = departmentId,
        CustodianId = custodianId,
        Quantity = 1,
        Status = MaterialStatus.InUse,
        CreatedAt = DateTime.UtcNow
    };

    private static SaveTestProjectRequest ProjectRequest(TestProject project, string name) => new()
    {
        Code = project.Code,
        Name = name,
        ProjectTypeCode = project.ProjectTypeCode,
        ProgressCode = project.ProgressCode,
        OwnerId = project.OwnerId,
        StartDate = project.StartDate,
        PlannedFinishDate = project.PlannedFinishDate,
        FollowUpIntervalDays = project.FollowUpIntervalDays,
        RowVersion = project.RowVersion
    };

    private async Task Login(string employeeNo)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { EmployeeNo = employeeNo, Password = "123456" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResult<LoginResponse>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
    }
}
