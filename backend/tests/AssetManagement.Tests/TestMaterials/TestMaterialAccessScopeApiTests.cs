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
        var workspace = await _client.GetFromJsonAsync<ApiResult<ProjectWorkspaceDto>>(
            $"/api/test-projects/{otherProject.Id}/workspace");
        var crossStage = await _client.PostAsJsonAsync(
            $"/api/test-projects/{otherProject.Id}/workspace/stages",
            new SaveProjectTestStageRequest { Name = "跨部门阶段", Sequence = 1 });
        var crossDelete = await _client.DeleteAsync($"/api/test-projects/{otherProject.Id}");

        crossUpdate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        crossProgress.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        workspace!.Data!.CanManage.Should().BeFalse();
        crossStage.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        crossDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

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
