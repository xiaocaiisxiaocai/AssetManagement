using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
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
    public async Task Create_material_rejects_inactive_department()
    {
        await Login();
        var project = await CreateProject("停用部门料件项目");
        var manager = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "停用部门负责人");
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = "已停用料件部门"
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{department.Data!.Id}", new UpdateDepartmentRequest
        {
            ManagerId = manager.Id,
            Name = department.Data.Name,
            IsActive = false
        });

        var res = await _client.PostAsJsonAsync("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "不应归属停用部门",
            ProjectId = project.Id,
            DepartmentId = department.Data.Id
        });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();

        body!.Code.Should().Be(4045);
        body.Message.Should().Be("部门不存在或已停用");
    }

    [Fact]
    public async Task Material_rejects_duplicate_name_in_same_project()
    {
        await Login();
        var project = await CreateProject("料件重名项目");
        var otherProject = await CreateProject("允许同名的其他项目");
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            ManagerId = 1,
            Name = $"重名料件部门-{Guid.NewGuid():N}"
        });
        var location = await Post<ApiResult<LocationNodeDto>>("/api/locations", new CreateLocationRequest
        {
            Name = $"重名料件位置-{Guid.NewGuid():N}"
        });
        var name = $"重名料件-{Guid.NewGuid():N}";
        await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(project.Id, name, department.Data!.Id, location.Data!.Id));

        var duplicateCreate = await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(project.Id, name, department.Data.Id, location.Data.Id));
        var sameNameInOtherProject = await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(otherProject.Id, name, department.Data.Id, location.Data.Id));
        var updateTarget = await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(project.Id, $"{name}-可更新", department.Data.Id, location.Data.Id));
        var duplicateUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/test-materials/{updateTarget.Data!.Id}",
            NewMaterialRequest(project.Id, name, department.Data.Id, location.Data.Id));
        var duplicateUpdate = await duplicateUpdateResponse.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();

        duplicateCreate.Code.Should().Be(4094);
        duplicateCreate.Message.Should().Be("料件名称已存在");
        sameNameInOtherProject.Code.Should().Be(0);
        duplicateUpdate!.Code.Should().Be(4094);
        duplicateUpdate.Message.Should().Be("料件名称已存在");
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
    public async Task Deleted_material_cannot_be_restored_while_its_project_is_deleted()
    {
        await Login();
        var project = await CreateProject("恢复关联校验项目");
        var material = (await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            ProjectId = project.Id,
            Name = "恢复关联校验料件"
        })).Data!;
        (await _client.DeleteAsync($"/api/test-materials/{material.Id}")).EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/test-projects/{project.Id}")).EnsureSuccessStatusCode();

        var blocked = await _client.PostAsync($"/api/test-materials/{material.Id}/restore", null);
        var blockedBody = await blocked.Content.ReadFromJsonAsync<ApiResult<object?>>();
        blockedBody!.Code.Should().Be(4094);

        (await _client.PostAsync($"/api/test-projects/{project.Id}/restore", null)).EnsureSuccessStatusCode();
        (await _client.PostAsync($"/api/test-materials/{material.Id}/restore", null)).EnsureSuccessStatusCode();
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
    public async Task Return_to_vendor_rejects_material_with_pending_flow()
    {
        await Login();
        await SetApprovalSwitch(true);
        try
        {
            var project = await CreateProject("待审批退回项目");
            var transferee = await CreateUserInDb($"tf{Guid.NewGuid():N}"[..12], "待审批接收人");
            var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
            {
                Name = "待审批退回样品",
                ProjectId = project.Id
            });
            await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
            {
                MaterialId = created.Data!.Id,
                TransfereeId = transferee.Id,
                Reason = "形成待审批流转"
            });

            var response = await _client.PostAsJsonAsync($"/api/test-materials/{created.Data.Id}/return", new { });
            var body = await response.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();
            var after = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>($"/api/test-materials/{created.Data.Id}");

            body!.Code.Should().Be(4092);
            body.Message.Should().Contain("进行中的流转");
            after!.Data!.Status.Should().Be(MaterialStatus.InUse);
            after.Data.HasPendingFlow.Should().BeTrue();
        }
        finally
        {
            await SetApprovalSwitch(false);
        }
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
    public async Task Project_owner_can_create_and_edit_material_without_material_permissions()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "普通负责人");
        var project = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest("负责人料件项目", owner.Id));

        await Login(owner.EmployeeNo, "123456");
        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "负责人新增样品",
            ProjectId = project.Data!.Id,
            VendorName = "供应商",
            Model = "M-1",
            Brand = "SAA",
            Quantity = 1,
            CustodianId = owner.Id,
            ReceivedDate = DateTime.UtcNow.Date
        });

        created.Data!.CustodianId.Should().Be(owner.Id);

        var updateResponse = await _client.PutAsJsonAsync($"/api/test-materials/{created.Data.Id}", new SaveTestMaterialRequest
        {
            Name = "负责人编辑样品",
            ProjectId = project.Data.Id,
            VendorName = "供应商",
            Model = "M-2",
            Brand = "SAA",
            Quantity = 2,
            CustodianId = owner.Id,
            ReceivedDate = DateTime.UtcNow.Date
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();
        updated!.Data!.Name.Should().Be("负责人编辑样品");
    }

    [Fact]
    public async Task Project_fields_options_and_followup_due_status_are_returned()
    {
        await Login();
        var owner = await CreateUserInDb("1901", "项目负责人");
        var project = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = "E2E整机测试",
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
        project.Data.NextFollowUpDueDate.Should().BeNull();
        project.Data.FollowUpStatus.Should().Be("upcoming");

        var options = await _client.GetFromJsonAsync<ApiResult<List<TestProjectOptionDto>>>(
            "/api/test-projects/options?kind=project_type");
        options!.Data!.Should().Contain(x => x.Code == "prototype" && x.Label == "样机测试");
    }

    [Fact]
    public async Task Project_rejects_duplicate_code_and_name_with_business_message()
    {
        await Login();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var code = $"TP-DUP-{suffix}";
        var name = $"重复项目-{suffix}";
        await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest(name, code: code));
        var another = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest($"{name}-其他", code: $"{code}-OTHER"));

        var duplicateCode = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest($"{name}-新名称", code: code));
        var duplicateName = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest(name, code: $"{code}-NEW"));
        var duplicateCodeOnUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{another.Data!.Id}",
            NewProjectRequest($"{name}-更新编号", code: code));
        var duplicateCodeOnUpdate = await duplicateCodeOnUpdateResponse.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();
        var duplicateNameOnUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{another.Data.Id}",
            NewProjectRequest(name, code: $"{code}-UPDATE"));
        var duplicateNameOnUpdate = await duplicateNameOnUpdateResponse.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        duplicateCode.Code.Should().Be(4094);
        duplicateCode.Message.Should().Be("项目编号已存在");
        duplicateName.Code.Should().Be(4094);
        duplicateName.Message.Should().Be("项目名称已存在");
        duplicateCodeOnUpdate!.Code.Should().Be(4094);
        duplicateCodeOnUpdate.Message.Should().Be("项目编号已存在");
        duplicateNameOnUpdate!.Code.Should().Be(4094);
        duplicateNameOnUpdate.Message.Should().Be("项目名称已存在");
    }

    [Fact]
    public async Task Project_rejects_missing_required_fields_on_create_and_update()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "必填项目负责人");
        var create = await _client.PostAsJsonAsync("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = $"必填校验-{Guid.NewGuid():N}"[..20],
            FollowUpIntervalDays = 14
        });
        var createBody = await create.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        var project = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = $"必填编辑-{Guid.NewGuid():N}"[..20],
            ProjectTypeCode = "prototype",
            ProgressCode = "testing",
            OwnerId = owner.Id,
            StartDate = new DateTime(2026, 6, 1),
            PlannedFinishDate = new DateTime(2026, 7, 1),
            FollowUpIntervalDays = 14
        });
        var update = await _client.PutAsJsonAsync($"/api/test-projects/{project.Data!.Id}", new SaveTestProjectRequest
        {
            Code = project.Data.Code,
            Name = project.Data.Name,
            FollowUpIntervalDays = 14
        });
        var updateBody = await update.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        createBody!.Code.Should().Be(4001);
        createBody.Message.Should().Contain("项目类型");
        updateBody!.Code.Should().Be(4001);
        updateBody.Message.Should().Contain("项目类型");
    }

    [Fact]
    public async Task Project_option_rejects_duplicate_kind_code_with_business_message()
    {
        await Login();
        var code = $"dup_{Guid.NewGuid():N}";
        await Post<ApiResult<TestProjectOptionDto>>("/api/test-projects/options", new SaveTestProjectOptionRequest
        {
            Kind = "project_type",
            Code = code,
            Label = "原配置",
            Sort = 1,
            IsActive = true
        });

        var duplicated = await Post<ApiResult<TestProjectOptionDto>>("/api/test-projects/options", new SaveTestProjectOptionRequest
        {
            Kind = "project_type",
            Code = code,
            Label = "重复配置",
            Sort = 2,
            IsActive = true
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("配置编码已存在");
    }

    [Fact]
    public async Task Project_option_used_by_project_cannot_be_deleted()
    {
        await Login();
        var option = await Post<ApiResult<TestProjectOptionDto>>("/api/test-projects/options", new SaveTestProjectOptionRequest
        {
            Kind = "project_type",
            Code = $"protect_{Guid.NewGuid():N}"[..20],
            Label = "被项目引用配置",
            Sort = 10,
            IsActive = true
        });
        await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest("引用项目配置项", projectTypeCode: option.Data!.Code));

        var deleted = await _client.DeleteAsync($"/api/test-projects/options/{option.Data.Id}");
        var body = await deleted.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4094);
        body.Message.Should().Contain("配置项已被项目使用");
    }

    [Fact]
    public async Task Material_with_flow_history_cannot_be_purged()
    {
        await Login();
        await SetApprovalSwitch(false);
        var project = await CreateProject("有流转历史料件项目");
        var material = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "有流转历史料件",
            ProjectId = project.Id
        });
        var transferee = await CreateUserInDb($"tf{Guid.NewGuid():N}"[..12], "流转接收人");
        var flow = await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Data!.Id,
            TransfereeId = transferee.Id,
            Reason = "保留历史"
        });
        await Post<ApiResult<MaterialFlowDto>>($"/api/material-flows/{flow.Data!.Id}/reject",
            new MaterialRejectRequest { NodeId = "dept_manager", Reason = "结束流程" });
        await _client.DeleteAsync($"/api/test-materials/{material.Data.Id}");

        var purged = await _client.DeleteAsync($"/api/test-materials/{material.Data.Id}/purge");
        var body = await purged.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4094);
        body.Message.Should().Contain("料件存在流转历史");
    }

    [Fact]
    public async Task Only_project_owner_or_admin_can_write_followup()
    {
        await Login();
        var manager = await CreateManagerInDb($"u{Guid.NewGuid():N}"[..12], "部门管理员");
        var outsider = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "无关员工");
        var project = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = "权限跟进项目",
            ProjectTypeCode = "prototype",
            ProgressCode = "landing",
            OwnerId = manager.Id,
            StartDate = DateTime.UtcNow.Date,
            PlannedFinishDate = DateTime.UtcNow.Date.AddDays(30),
            FollowUpIntervalDays = 14
        });

        // 非项目负责人也不是管理员，应被业务权限拒绝
        await Login(outsider.EmployeeNo, "123456");
        var denied = await _client.PostAsJsonAsync($"/api/test-projects/{project.Data!.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "我不应该能填" });
        var deniedBody = await denied.Content.ReadFromJsonAsync<ApiResult<TestProjectFollowupDto>>();
        deniedBody!.Code.Should().Be(4031);

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

    [Fact]
    public async Task Followup_is_available_only_when_project_is_landing()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "落地负责人");
        var planned = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = "未落地项目",
            ProjectTypeCode = "prototype",
            ProgressCode = "planning",
            OwnerId = owner.Id,
            StartDate = new DateTime(2026, 6, 29),
            PlannedFinishDate = new DateTime(2026, 7, 29),
            FollowUpIntervalDays = 7
        });

        planned.Data!.NextFollowUpDueDate.Should().BeNull();
        planned.Data.CanWriteFollowUp.Should().BeFalse();

        await Login(owner.EmployeeNo, "123456");
        var denied = await _client.PostAsJsonAsync($"/api/test-projects/{planned.Data.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "未落地不应允许填写" });
        var deniedBody = await denied.Content.ReadFromJsonAsync<ApiResult<TestProjectFollowupDto>>();
        deniedBody!.Code.Should().Be(4031);

        await Login();
        var landing = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = "落地项目",
            ProjectTypeCode = "prototype",
            ProgressCode = "landing",
            OwnerId = owner.Id,
            StartDate = new DateTime(2026, 6, 29),
            PlannedFinishDate = new DateTime(2026, 7, 29),
            FollowUpIntervalDays = 7
        });

        landing.Data!.NextFollowUpDueDate.Should().Be(new DateTime(2026, 7, 6));
        landing.Data.CanWriteFollowUp.Should().BeFalse();

        await Login(owner.EmployeeNo, "123456");
        var saved = await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "落地后填写" });
        saved.Data!.Content.Should().Be("落地后填写");
    }

    [Fact]
    public async Task Followup_create_update_delete_updates_list_and_project_summary()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "跟进维护负责人");
        var landing = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = "跟进维护项目",
            ProjectTypeCode = "prototype",
            ProgressCode = "landing",
            OwnerId = owner.Id,
            StartDate = DateTime.UtcNow.Date.AddDays(-1),
            PlannedFinishDate = DateTime.UtcNow.Date.AddDays(30),
            FollowUpIntervalDays = 7
        });

        await Login(owner.EmployeeNo, "123456");
        var created = await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data!.Id}/followups",
            new SaveTestProjectFollowupRequest
            {
                Content = "第一轮落地情况",
                DueDate = new DateTime(2026, 7, 10)
            });
        var updated = await Put<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data.Id}/followups/{created.Data!.Id}",
            new SaveTestProjectFollowupRequest
            {
                Content = "第二轮落地情况",
                DueDate = new DateTime(2026, 7, 17)
            });
        var list = await _client.GetFromJsonAsync<ApiResult<List<TestProjectFollowupDto>>>(
            $"/api/test-projects/{landing.Data.Id}/followups");
        var projects = await _client.GetFromJsonAsync<ApiResult<List<TestProjectDto>>>("/api/test-projects");
        var projectSummary = projects!.Data!.Single(x => x.Id == landing.Data.Id);

        updated.Data!.Content.Should().Be("第二轮落地情况");
        updated.Data.DueDate.Should().Be(new DateTime(2026, 7, 17));
        list!.Data!.Should().ContainSingle(x => x.Id == created.Data.Id && x.Content == "第二轮落地情况");
        projectSummary.LatestFollowUpContent.Should().Be("第二轮落地情况");
        projectSummary.NextFollowUpDueDate.Should().Be(DateTime.UtcNow.Date.AddDays(7));

        var deleted = await _client.DeleteAsync($"/api/test-projects/{landing.Data.Id}/followups/{created.Data.Id}");
        var afterDelete = await _client.GetFromJsonAsync<ApiResult<List<TestProjectFollowupDto>>>(
            $"/api/test-projects/{landing.Data.Id}/followups");

        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        afterDelete!.Data!.Should().NotContain(x => x.Id == created.Data.Id);
    }

    // ===== 辅助方法 =====
    private async Task<TestProjectDto> CreateProject(string name)
        => (await Post<ApiResult<TestProjectDto>>("/api/test-projects", NewProjectRequest(name))).Data!;

    private static SaveTestProjectRequest NewProjectRequest(
        string name,
        int? ownerId = 1,
        string? code = null,
        string projectTypeCode = "prototype",
        string progressCode = "testing") => new()
        {
            Code = code ?? NewProjectCode(),
            FollowUpIntervalDays = 14,
            Name = name,
            OwnerId = ownerId,
            PlannedFinishDate = new DateTime(2026, 7, 29),
            ProgressCode = progressCode,
            ProjectTypeCode = projectTypeCode,
            StartDate = new DateTime(2026, 6, 29)
        };

    private static string NewProjectCode() => $"TP-{Guid.NewGuid():N}"[..20];

    private static SaveTestMaterialRequest NewMaterialRequest(
        int projectId,
        string name,
        int departmentId,
        int locationId) => new()
    {
        Brand = "SAA",
        DepartmentId = departmentId,
        CustodianId = 3,
        LocationId = locationId,
        Model = "TM-Model",
        Name = name,
        ProjectId = projectId,
        Quantity = 1,
        ReceivedDate = DateTime.UtcNow.Date,
        VendorName = "测试供应商"
    };

    private async Task Login(string employeeNo = "1001", string password = "123456")
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task SetApprovalSwitch(bool enabled)
    {
        await Put<ApiResult<object?>>("/api/settings", new[]
        {
            new SaveSystemSettingRequest
            {
                Key = "material.transfer.approval.enabled",
                Value = enabled ? "true" : "false",
                Description = "是否启用测试料件转移审批(false=直接转移)"
            }
        });
    }

    private async Task<User> CreateManagerInDb(string employeeNo, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var managerRole = db.Roles.Single(x => x.Code == "supervisor");
        var department = db.Departments.FirstOrDefault(x => x.IsActive);
        if (department is null)
        {
            department = new Department
            {
                Code = $"D{Guid.NewGuid():N}"[..12],
                Name = "测试管理部门",
                IsActive = true
            };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
        }
        var user = new User
        {
            EmployeeNo = employeeNo,
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsActive = true,
            DepartmentId = department.Id
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

    private async Task<T> Put<T>(string url, object payload)
    {
        var res = await _client.PutAsJsonAsync(url, payload);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
