using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssetManagement.Tests.TestMaterials;

public class TestMaterialApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;
    public TestMaterialApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_material_autogenerates_no_and_lists()
    {
        await Login();
        var project = await CreateProject("电池测试项目");

        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "锂电池样品",
            ProjectId = project.Id,
            VendorName = "宁德时代",
            Model = "LFP-280",
            Brand = "CATL",
            Quantity = 10
        });

        created.Data!.MaterialNo.Should().StartWith("TM-");
        created.Data.ProjectName.Should().Be("电池测试项目");
        created.Data.Status.Should().Be(MaterialStatus.InUse);

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestMaterialDto>>>(
            $"/api/test-materials?projectId={project.Id}");
        list!.Data!.Items.Should().Contain(x => x.Id == created.Data.Id);
    }

    [Fact]
    public async Task Soft_delete_keeps_in_all_list_and_restore_brings_back_active()
    {
        await Login();
        var project = await CreateProject("软删除项目");
        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "待删样品", ProjectId = project.Id
        });
        var id = created.Data!.Id;

        (await _client.DeleteAsync($"/api/test-materials/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var activeList = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestMaterialDto>>>(
            "/api/test-materials?deleteStatus=active");
        activeList!.Data!.Items.Should().NotContain(x => x.Id == id);

        var allList = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestMaterialDto>>>(
            "/api/test-materials?deleteStatus=all");
        allList!.Data!.Items.Should().Contain(x => x.Id == id && x.IsDeleted);

        // 详情允许查看已删除
        var detail = await _client.GetFromJsonAsync<ApiResult<TestMaterialDetailDto>>(
            $"/api/test-materials/{id}/detail");
        detail!.Data!.Material.IsDeleted.Should().BeTrue();

        // 撤销删除
        (await _client.PostAsync($"/api/test-materials/{id}/restore", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterRestore = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestMaterialDto>>>(
            "/api/test-materials?deleteStatus=active");
        afterRestore!.Data!.Items.Should().Contain(x => x.Id == id);
    }

    [Fact]
    public async Task Project_with_materials_cannot_be_deleted()
    {
        await Login();
        var project = await CreateProject("占用项目");
        await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "占位样品", ProjectId = project.Id
        });

        var resp = await _client.DeleteAsync($"/api/test-projects/{project.Id}");
        var body = await resp.Content.ReadFromJsonAsync<ApiResult<object>>();
        body!.Code.Should().NotBe(0); // 业务异常:项目下仍有料件
    }

    [Fact]
    public async Task Return_to_vendor_changes_status()
    {
        await Login();
        var project = await CreateProject("退回项目");
        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "退回样品", ProjectId = project.Id
        });
        var returned = await Post<ApiResult<TestMaterialDto>>($"/api/test-materials/{created.Data!.Id}/return", new { });
        returned.Data!.Status.Should().Be(MaterialStatus.ReturnedToVendor);
    }

    [Fact]
    public async Task Returned_material_cannot_be_updated_or_deleted()
    {
        await Login();
        var project = await CreateProject("退回锁定项目");
        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "退回后锁定样品", ProjectId = project.Id
        });
        await Post<ApiResult<TestMaterialDto>>($"/api/test-materials/{created.Data!.Id}/return", new { });

        var updateResponse = await _client.PutAsJsonAsync($"/api/test-materials/{created.Data.Id}", new SaveTestMaterialRequest
        {
            Name = "不应允许修改", ProjectId = project.Id
        });
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();
        updateBody!.Code.Should().Be(4098);

        var deleteResponse = await _client.DeleteAsync($"/api/test-materials/{created.Data.Id}");
        var deleteBody = await deleteResponse.Content.ReadFromJsonAsync<ApiResult<object>>();
        deleteBody!.Code.Should().Be(4098);
    }

    [Fact]
    public async Task Project_fields_options_and_followup_due_status_are_returned()
    {
        await Login();
        var owner = await CreateUserInDb("1901", "项目负责人");
        var project = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Name = "E2E整机测试",
            Code = "TP-001",
            ProjectTypeCode = "prototype",
            ProgressCode = "testing",
            OwnerId = owner.Id,
            StartDate = new DateTime(2026, 6, 1),
            PlannedFinishDate = new DateTime(2026, 7, 1),
            ClosedDate = new DateTime(2026, 7, 15),
            FollowUpIntervalDays = 14,
            TestStatus = "样机测试中"
        });

        project.Data!.ProjectTypeLabel.Should().Be("样机测试");
        project.Data.ProgressLabel.Should().Be("测试中");
        project.Data.OwnerName.Should().Be("项目负责人");
        project.Data.FollowUpIntervalDays.Should().Be(14);
        project.Data.NextFollowUpDueDate.Should().Be(new DateTime(2026, 6, 15));
        project.Data.FollowUpStatus.Should().NotBeNullOrWhiteSpace();

        var options = await _client.GetFromJsonAsync<ApiResult<List<TestProjectOptionDto>>>(
            "/api/test-projects/options?kind=project_type");
        options!.Data!.Should().Contain(x => x.Code == "prototype" && x.Label == "样机测试");
    }

    [Fact]
    public async Task Only_project_owner_or_admin_can_write_followup()
    {
        await Login();
        var manager = await CreateManagerInDb("1902", "部门管理员");
        var outsider = await CreateUserInDb("1903", "无关员工");
        var project = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Name = "权限跟进项目",
            ProjectTypeCode = "prototype",
            ProgressCode = "testing",
            OwnerId = manager.Id,
            StartDate = DateTime.UtcNow.Date,
            FollowUpIntervalDays = 14
        });

        // 无 project:manage 权限的普通员工应被拒绝（403）
        await Login(outsider.EmployeeNo, "123456");
        var denied = await _client.PostAsJsonAsync($"/api/test-projects/{project.Data!.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "我不应该能填" });
        denied.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);

        // 有 project:manage 权限的部门管理员可以写跟进
        await Login(manager.EmployeeNo, "123456");
        var managerFollowup = await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{project.Data.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "管理员填写本期落地情况" });
        managerFollowup.Data!.FilledByName.Should().Be("部门管理员");

        // 系统管理员也可以写跟进
        await Login();
        var adminFollowup = await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{project.Data.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "管理员补充跟进" });
        adminFollowup.Data!.FilledByName.Should().Be("系统管理员");
    }

    // ===== 辅助方法 =====
    private async Task<TestProjectDto> CreateProject(string name)
        => (await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest { Name = name })).Data!;

    private async Task Login(string employeeNo = "1001", string password = "123456")
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task<User> CreateManagerInDb(string employeeNo, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var managerRole = db.Roles.Single(x => x.Code == "dept_admin");
        var user = new User
        {
            EmployeeNo = employeeNo,
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = managerRole.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<User> CreateUserInDb(string employeeNo, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var employeeRole = db.Roles.Single(x => x.Code == "employee");
        var user = new User
        {
            EmployeeNo = employeeNo,
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = employeeRole.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<T> Post<T>(string url, object payload)
    {
        var res = await _client.PostAsJsonAsync(url, payload);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
