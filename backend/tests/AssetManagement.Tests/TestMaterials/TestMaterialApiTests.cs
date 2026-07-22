using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Xml.Linq;
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
            Quantity = 10,
            LocationName = "  二楼实验室 B-08  "
        });

        created.Data!.MaterialNo.Should().StartWith("TM-");
        created.Data.ProjectName.Should().Be("电池测试项目");
        created.Data.LocationName.Should().Be("二楼实验室 B-08");
        created.Data.Status.Should().Be(MaterialStatus.InUse);

        var list = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestMaterialDto>>>(
            $"/api/test-materials?projectId={project.Id}");
        list!.Data!.Items.Should().Contain(x => x.Id == created.Data.Id);
    }

    [Fact]
    public async Task Project_page_applies_server_filters_and_total_before_paging()
    {
        await Login();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateProject($"分页项目-{marker}-一");
        await CreateProject($"分页项目-{marker}-二");
        await CreateProject($"分页项目-{marker}-三");

        var first = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestProjectDto>>>(
            $"/api/test-projects/page?name={marker}&page=1&pageSize=2");
        var second = await _client.GetFromJsonAsync<ApiResult<PagedResult<TestProjectDto>>>(
            $"/api/test-projects/page?name={marker}&page=2&pageSize=2");

        first!.Data!.Total.Should().Be(3);
        first.Data.Items.Should().HaveCount(2);
        second!.Data!.Total.Should().Be(3);
        second.Data.Items.Should().ContainSingle();
        second.Data.Items.Should().OnlyContain(x => x.Name.Contains(marker));
    }

    [Fact]
    public async Task Create_material_rejects_inactive_department()
    {
        await Login();
        var project = await CreateProject("停用部门料件项目");
        var department = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest
        {
            Name = "已停用料件部门"
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{department.Data!.Id}", new UpdateDepartmentRequest
        {
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
    public async Task Create_material_rejects_well_formed_but_missing_stored_image()
    {
        await Login();
        var project = await CreateProject("料件图片完整性项目");

        var response = await _client.PostAsJsonAsync("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "引用不存在图片的料件",
            ProjectId = project.Id,
            Images = new List<string> { $"/api/files/{Guid.NewGuid():N}.png" }
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();

        body!.Code.Should().Be(4152);
        body.Message.Should().Contain("不存在");
    }

    [Fact]
    public async Task Create_material_image_reference_uses_the_same_trimmed_url_as_persistence()
    {
        await Login();
        var project = await CreateProject("料件图片地址规范化项目");
        var uploadedUrl = await UploadImage();

        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "图片地址规范化料件",
            ProjectId = project.Id,
            Images = new List<string> { $"  {uploadedUrl}  " },
        });

        created.Code.Should().Be(0, created.Message);
        created.Data!.Images.Should().Equal(uploadedUrl);
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
        var name = $"重名料件-{Guid.NewGuid():N}";
        await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(project.Id, name, department.Data!.Id));

        var duplicateCreate = await PostError<TestMaterialDto>(
            "/api/test-materials",
            NewMaterialRequest(project.Id, name, department.Data.Id),
            HttpStatusCode.Conflict);
        var sameNameInOtherProject = await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(otherProject.Id, name, department.Data.Id));
        var updateTarget = await Post<ApiResult<TestMaterialDto>>(
            "/api/test-materials",
            NewMaterialRequest(project.Id, $"{name}-可更新", department.Data.Id));
        var duplicateUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/test-materials/{updateTarget.Data!.Id}",
            NewMaterialRequest(project.Id, name, department.Data.Id));
        duplicateUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicateUpdate = await duplicateUpdateResponse.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();

        duplicateCreate.Code.Should().Be(4094);
        duplicateCreate.Message.Should().Be("料件名称已存在");
        sameNameInOtherProject.Code.Should().Be(0);
        duplicateUpdate!.Code.Should().Be(4094);
        duplicateUpdate.Message.Should().Be("料件名称已存在");
    }

    [Fact]
    public async Task Material_update_cannot_change_its_project()
    {
        await Login();
        var project = await CreateProject("料件原项目");
        var otherProject = await CreateProject("料件目标项目");
        var created = await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = "项目不可变料件",
            ProjectId = project.Id
        });

        var response = await _client.PutAsJsonAsync(
            $"/api/test-materials/{created.Data!.Id}",
            new SaveTestMaterialRequest
            {
                Name = created.Data.Name,
                ProjectId = otherProject.Id,
                Quantity = created.Data.Quantity
            });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<TestMaterialDto>>();

        body!.Code.Should().Be(4095);
        body.Message.Should().Be("料件所属项目不能修改");
        var current = await _client.GetFromJsonAsync<ApiResult<TestMaterialDto>>(
            $"/api/test-materials/{created.Data.Id}");
        current!.Data!.ProjectId.Should().Be(project.Id);
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

        var detail = await _client.GetFromJsonAsync<ApiResult<TestMaterialDetailDto>>(
            $"/api/test-materials/{created.Data.Id}/detail");
        var returnRecord = detail!.Data!.Records.Should()
            .ContainSingle(x => x.Action == "return_to_vendor").Which;
        returnRecord.Key.Should().StartWith("material:");
        returnRecord.Operator.Should().Be("系统管理员");
        returnRecord.OperatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
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
    public async Task Project_owner_and_admin_can_update_progress_but_outsider_is_denied()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "进展负责人");
        var outsider = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "无关员工");
        var project = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest("负责人更新进展项目", owner.Id));
        var closedDate = new DateTime(2026, 7, 20);

        await Login(owner.EmployeeNo, "123456");
        var updatedResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{project.Data!.Id}/progress",
            new
            {
                ProgressCode = "closed",
                ClosedDate = closedDate,
                TestStatus = "负责人确认测试完成"
            });
        updatedResponse.EnsureSuccessStatusCode();
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        updated!.Data!.ProgressCode.Should().Be("closed");
        updated.Data.ClosedDate.Should().Be(closedDate);
        updated.Data.TestStatus.Should().Be("负责人确认测试完成");
        updated.Data.Name.Should().Be(project.Data.Name);
        updated.Data.Code.Should().Be(project.Data.Code);
        updated.Data.OwnerId.Should().Be(owner.Id);

        var fullUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{project.Data.Id}",
            NewProjectRequest("负责人不应修改基础信息", owner.Id));
        fullUpdate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Login(outsider.EmployeeNo, "123456");
        var deniedResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{project.Data.Id}/progress",
            new
            {
                ProgressCode = "testing",
                ClosedDate = (DateTime?)null,
                TestStatus = "无关员工不应修改"
            });
        var denied = await deniedResponse.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();
        denied!.Code.Should().Be(4031);

        await Login();
        var adminResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{project.Data.Id}/progress",
            new
            {
                ProgressCode = "testing",
                ClosedDate = (DateTime?)null,
                TestStatus = "管理员调整项目进展"
            });
        var adminUpdated = await adminResponse.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        adminResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        adminUpdated!.Code.Should().Be(4094);
        adminUpdated.Message.Should().Contain("已结案");

        var fullAdminUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{project.Data.Id}",
            NewProjectRequest("结案后不应修改", owner.Id, project.Data.Code));
        var fullAdminUpdateBody = await fullAdminUpdate.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();
        fullAdminUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        fullAdminUpdateBody!.Code.Should().Be(4094);
        fullAdminUpdateBody.Message.Should().Contain("已结案");
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

        var duplicateCode = await PostError<TestProjectDto>(
            "/api/test-projects",
            NewProjectRequest($"{name}-新名称", code: code),
            HttpStatusCode.Conflict);
        var duplicateName = await PostError<TestProjectDto>(
            "/api/test-projects",
            NewProjectRequest(name, code: $"{code}-NEW"),
            HttpStatusCode.Conflict);
        var duplicateCodeOnUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{another.Data!.Id}",
            NewProjectRequest($"{name}-更新编号", code: code));
        duplicateCodeOnUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicateCodeOnUpdate = await duplicateCodeOnUpdateResponse.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();
        var duplicateNameOnUpdateResponse = await _client.PutAsJsonAsync(
            $"/api/test-projects/{another.Data.Id}",
            NewProjectRequest(name, code: $"{code}-UPDATE"));
        duplicateNameOnUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
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
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var updateBody = await update.Content.ReadFromJsonAsync<ApiResult<TestProjectDto>>();

        createBody!.Code.Should().Be(4001);
        createBody.Message.Should().MatchRegex("项目类型|项目进度");
        updateBody!.Code.Should().Be(4001);
        updateBody.Message.Should().MatchRegex("项目类型|项目进度");
    }

    [Fact]
    public async Task Project_rejects_invalid_timeline_and_closed_state()
    {
        await Login();

        var reversedRequest = NewProjectRequest("时间倒置项目");
        reversedRequest.StartDate = new DateTime(2026, 7, 2);
        reversedRequest.PlannedFinishDate = new DateTime(2026, 7, 1);
        var reversed = await PostError<TestProjectDto>(
            "/api/test-projects",
            reversedRequest,
            HttpStatusCode.BadRequest);
        var closedWithoutDate = await PostError<TestProjectDto>(
            "/api/test-projects",
            NewProjectRequest("无结案日期项目", progressCode: "closed"),
            HttpStatusCode.BadRequest);
        var testingWithClosedDateRequest = NewProjectRequest("测试中误填结案日期");
        testingWithClosedDateRequest.ClosedDate = new DateTime(2026, 7, 1);
        var testingWithClosedDate = await PostError<TestProjectDto>(
            "/api/test-projects",
            testingWithClosedDateRequest,
            HttpStatusCode.BadRequest);

        reversed.Code.Should().Be(4001);
        reversed.Message.Should().Contain("计划完成时间不能早于开始时间");
        closedWithoutDate.Code.Should().Be(4001);
        closedWithoutDate.Message.Should().Contain("必须填写结案时间");
        testingWithClosedDate.Code.Should().Be(4001);
        testingWithClosedDate.Message.Should().Contain("只有已结案项目");
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

        var duplicated = await PostError<TestProjectOptionDto>("/api/test-projects/options", new SaveTestProjectOptionRequest
        {
            Kind = "project_type",
            Code = code,
            Label = "重复配置",
            Sort = 2,
            IsActive = true
        }, HttpStatusCode.Conflict);

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
        deleted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await deleted.Content.ReadFromJsonAsync<ApiResult<object?>>();

        body!.Code.Should().Be(4094);
        body.Message.Should().Contain("配置项已被项目使用");
    }

    [Fact]
    public async Task Project_option_used_by_project_cannot_be_disabled_or_rekeyed()
    {
        await Login();
        var option = await Post<ApiResult<TestProjectOptionDto>>("/api/test-projects/options", new SaveTestProjectOptionRequest
        {
            Kind = "project_type",
            Code = $"used_{Guid.NewGuid():N}"[..20],
            Label = "使用中的配置",
            Sort = 10,
            IsActive = true
        });
        await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest("配置保护项目", projectTypeCode: option.Data!.Code));

        var disabled = await PutError<TestProjectOptionDto>(
            $"/api/test-projects/options/{option.Data.Id}",
            new SaveTestProjectOptionRequest
            {
                Kind = option.Data.Kind,
                Code = option.Data.Code,
                Label = option.Data.Label,
                Sort = option.Data.Sort,
                IsActive = false
            }, HttpStatusCode.Conflict);
        var rekeyed = await PutError<TestProjectOptionDto>(
            $"/api/test-projects/options/{option.Data.Id}",
            new SaveTestProjectOptionRequest
            {
                Kind = option.Data.Kind,
                Code = $"new_{Guid.NewGuid():N}"[..20],
                Label = option.Data.Label,
                Sort = option.Data.Sort,
                IsActive = true
            }, HttpStatusCode.Conflict);

        disabled.Code.Should().Be(4094);
        disabled.Message.Should().Contain("不能停用");
        rekeyed.Code.Should().Be(4094);
        rekeyed.Message.Should().Contain("不能修改类型或编码");
    }

    [Fact]
    public async Task Reserved_project_progress_cannot_be_deleted_disabled_or_rekeyed()
    {
        await Login();
        var options = await _client.GetFromJsonAsync<ApiResult<List<TestProjectOptionDto>>>(
            "/api/test-projects/options?kind=project_progress");
        var landing = options!.Data!.Single(x => x.Code == "landing");

        var deleted = await _client.DeleteAsync($"/api/test-projects/options/{landing.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var deletedBody = await deleted.Content.ReadFromJsonAsync<ApiResult<object?>>();
        var disabled = await PutError<TestProjectOptionDto>(
            $"/api/test-projects/options/{landing.Id}",
            new SaveTestProjectOptionRequest
            {
                Kind = landing.Kind,
                Code = landing.Code,
                Label = landing.Label,
                Sort = landing.Sort,
                IsActive = false
            }, HttpStatusCode.Conflict);
        var rekeyed = await PutError<TestProjectOptionDto>(
            $"/api/test-projects/options/{landing.Id}",
            new SaveTestProjectOptionRequest
            {
                Kind = landing.Kind,
                Code = "landing_changed",
                Label = landing.Label,
                Sort = landing.Sort,
                IsActive = true
            }, HttpStatusCode.Conflict);

        deletedBody!.Code.Should().Be(4094);
        disabled.Code.Should().Be(4094);
        rekeyed.Code.Should().Be(4094);
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
        await Post<ApiResult<MaterialFlowDto>>("/api/material-flows", new InitiateTransferRequest
        {
            MaterialId = material.Data!.Id,
            TransfereeId = transferee.Id,
            Reason = "保留历史"
        });
        await _client.DeleteAsync($"/api/test-materials/{material.Data.Id}");

        var purged = await _client.DeleteAsync($"/api/test-materials/{material.Data.Id}/purge");
        purged.StatusCode.Should().Be(HttpStatusCode.Conflict);
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
        projectSummary.NextFollowUpDueDate.Should().Be(new DateTime(2026, 7, 24));

        var deleted = await _client.DeleteAsync($"/api/test-projects/{landing.Data.Id}/followups/{created.Data.Id}");
        var afterDelete = await _client.GetFromJsonAsync<ApiResult<List<TestProjectFollowupDto>>>(
            $"/api/test-projects/{landing.Data.Id}/followups");

        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        afterDelete!.Data!.Should().NotContain(x => x.Id == created.Data.Id);
    }

    [Fact]
    public async Task Followup_create_and_update_reject_future_business_date()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "未来日期负责人");
        var landing = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest("未来日期跟进项目", owner.Id, progressCode: "landing"));

        await Login(owner.EmployeeNo, "123456");
        var valid = await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data!.Id}/followups",
            new SaveTestProjectFollowupRequest
            {
                Content = "今日已发生的进展",
                DueDate = BusinessClock.Today
            });

        var futureCreate = await _client.PostAsJsonAsync(
            $"/api/test-projects/{landing.Data.Id}/followups",
            new SaveTestProjectFollowupRequest
            {
                Content = "不应预写的进展",
                DueDate = BusinessClock.Today.AddDays(1)
            });
        var futureCreateBody = await futureCreate.Content.ReadFromJsonAsync<ApiResult<TestProjectFollowupDto>>();
        futureCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        futureCreateBody!.Message.Should().Contain("不能晚于今天");

        var futureUpdate = await _client.PutAsJsonAsync(
            $"/api/test-projects/{landing.Data.Id}/followups/{valid.Data!.Id}",
            new SaveTestProjectFollowupRequest
            {
                Content = "不应改成未来日期",
                DueDate = BusinessClock.Today.AddDays(1)
            });
        var futureUpdateBody = await futureUpdate.Content.ReadFromJsonAsync<ApiResult<TestProjectFollowupDto>>();
        futureUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        futureUpdateBody!.Message.Should().Contain("不能晚于今天");
    }

    [Fact]
    public async Task Project_export_contains_filtered_projects_and_corresponding_materials_with_remark()
    {
        await Login();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var project = await CreateProject($"导出项目-{marker}");
        await Post<ApiResult<TestMaterialDto>>("/api/test-materials", new SaveTestMaterialRequest
        {
            Name = $"导出料件-{marker}",
            ProjectId = project.Id,
            VendorName = "导出厂商",
            Quantity = 2,
            Remark = $"导出备注-{marker}"
        });

        var response = await _client.GetAsync($"/api/test-projects/export?name={marker}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var workbook = ReadZipXml(archive, "xl/workbook.xml");
        var sheetNames = workbook.Descendants()
            .Where(x => x.Name.LocalName == "sheet")
            .Select(x => (string?)x.Attribute("name"))
            .ToArray();
        sheetNames.Should().Equal("测试项目", "测试料件");

        var allWorksheetText = string.Join("\n", archive.Entries
            .Where(x => x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal))
            .Select(entry => ReadZipXml(entry).ToString()));
        allWorksheetText.Should().Contain(project.Name);
        allWorksheetText.Should().Contain($"导出料件-{marker}");
        allWorksheetText.Should().Contain($"导出备注-{marker}");
    }

    [Fact]
    public async Task Editing_old_followup_preserves_author_and_does_not_change_latest_cycle()
    {
        await Login();
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "历史跟进负责人");
        var landing = await Post<ApiResult<TestProjectDto>>("/api/test-projects", new SaveTestProjectRequest
        {
            Code = NewProjectCode(),
            Name = $"历史跟进-{Guid.NewGuid():N}"[..20],
            ProjectTypeCode = "prototype",
            ProgressCode = "landing",
            OwnerId = owner.Id,
            StartDate = new DateTime(2026, 7, 1),
            PlannedFinishDate = new DateTime(2026, 8, 1),
            FollowUpIntervalDays = 7
        });

        await Login(owner.EmployeeNo, "123456");
        var oldFollowup = await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data!.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "较早周期", DueDate = new DateTime(2026, 7, 10) });
        await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "最新周期", DueDate = new DateTime(2026, 7, 20) });

        await Login();
        var edited = await Put<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data.Id}/followups/{oldFollowup.Data!.Id}",
            new SaveTestProjectFollowupRequest { Content = "修正较早周期", DueDate = new DateTime(2026, 7, 10) });
        var projects = await _client.GetFromJsonAsync<ApiResult<List<TestProjectDto>>>("/api/test-projects");
        var summary = projects!.Data!.Single(x => x.Id == landing.Data.Id);

        edited.Data!.FilledById.Should().Be(owner.Id);
        edited.Data.FilledByName.Should().Be("历史跟进负责人");
        summary.LatestFollowUpContent.Should().Be("最新周期");
        summary.NextFollowUpDueDate.Should().Be(new DateTime(2026, 7, 27));
    }

    [Fact]
    public async Task Project_stats_keep_progress_buckets_exclusive_and_count_followup_records()
    {
        await Login();
        var baseline = await _client.GetFromJsonAsync<ApiResult<TestProjectStatsDto>>("/api/test-projects/stats");
        var owner = await CreateUserInDb($"u{Guid.NewGuid():N}"[..12], "统计项目负责人");
        await Post<ApiResult<TestProjectDto>>("/api/test-projects", NewProjectRequest("统计测试中项目", owner.Id));
        var landing = await Post<ApiResult<TestProjectDto>>(
            "/api/test-projects",
            NewProjectRequest("统计落地跟进项目", owner.Id, progressCode: "landing"));
        var closedRequest = NewProjectRequest("统计结案项目", owner.Id, progressCode: "closed");
        closedRequest.ClosedDate = DateTime.UtcNow.Date;
        await Post<ApiResult<TestProjectDto>>("/api/test-projects", closedRequest);

        await Login(owner.EmployeeNo, "123456");
        await Post<ApiResult<TestProjectFollowupDto>>(
            $"/api/test-projects/{landing.Data!.Id}/followups",
            new SaveTestProjectFollowupRequest { Content = "统计本月跟进", DueDate = DateTime.UtcNow.Date });
        var after = await _client.GetFromJsonAsync<ApiResult<TestProjectStatsDto>>("/api/test-projects/stats");
        var month = DateTime.UtcNow.Month;

        after!.Data!.Total.Should().Be(baseline!.Data!.Total + 3);
        after.Data.Closed.Should().Be(baseline.Data.Closed + 1);
        after.Data.Landed.Should().Be(baseline.Data.Landed + 1);
        after.Data.InProgress.Should().Be(baseline.Data.InProgress + 1);
        after.Data.MonthlyStat.Single(x => x.Month == month).ClosedCount.Should()
            .Be(baseline.Data.MonthlyStat.Single(x => x.Month == month).ClosedCount + 1);
        after.Data.MonthlyStat.Single(x => x.Month == month).FollowUpCount.Should()
            .Be(baseline.Data.MonthlyStat.Single(x => x.Month == month).FollowUpCount + 1);
    }

      // ===== 辅助方法 =====
    private static XDocument ReadZipXml(ZipArchive archive, string path)
        => ReadZipXml(archive.GetEntry(path) ?? throw new InvalidDataException($"Excel 缺少 {path}"));

    private static XDocument ReadZipXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

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
        int departmentId) => new()
    {
        Brand = "SAA",
        DepartmentId = departmentId,
        LocationName = "二楼实验室 B-08",
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

    private async Task<string> UploadImage()
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 });
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "material.png");
        var response = await _client.PostAsync("/api/files/upload", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<
            ApiResult<AssetManagement.Application.Files.FileUploadResult>>())!.Data!.Url;
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

    private async Task<ApiResult<T>> PostError<T>(string url, object payload, HttpStatusCode expectedStatus)
    {
        var response = await _client.PostAsJsonAsync(url, payload);
        response.StatusCode.Should().Be(expectedStatus);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!;
    }

    private async Task<ApiResult<T>> PutError<T>(string url, object payload, HttpStatusCode expectedStatus)
    {
        var response = await _client.PutAsJsonAsync(url, payload);
        response.StatusCode.Should().Be(expectedStatus);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!;
    }
}
