using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Api.Controllers;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Auth;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Net;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Tests.Workflow;

public class WorkflowCrudApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WorkflowCrudApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Workflow_can_create_update_and_delete()
    {
        await Login();
        var bizType = Unique("custom");

        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "自定义流程",
            BizType = bizType
        });

        created.Code.Should().Be(0);
        created.Data!.Id.Should().BeGreaterThan(0);
        created.Data.Name.Should().Be("自定义流程");
        created.Data.BizType.Should().Be(bizType);

        var updated = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data.Id}", new SaveWorkflowRequest
        {
            Name = "自定义流程-已修改",
            BizType = $"{bizType}_edit"
        });

        updated.Code.Should().Be(0);
        updated.Data!.Name.Should().Be("自定义流程-已修改");
        updated.Data.BizType.Should().Be($"{bizType}_edit");

        var deleteResponse = await _client.DeleteAsync($"/api/workflows/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiResult<bool>>();
        deleted!.Code.Should().Be(0);
        deleted.Data.Should().BeTrue();

        var getDeletedResponse = await _client.GetAsync($"/api/workflows/{created.Data.Id}");
        getDeletedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var getDeleted = await getDeletedResponse.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();
        getDeleted!.Code.Should().Be(4049);
        getDeleted.Message.Should().Be("流程不存在");
    }

    [Fact]
    public async Task Workflow_list_returns_display_label_and_validated_bpmn_status()
    {
        await Login();
        var empty = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "未配置流程",
            BizType = Unique("empty")
        });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var materialWorkflow = db.Workflows.AsTracking().Single(x => x.BizType == "material_transfer");
            materialWorkflow.BpmnXml = "<invalid>";
            await db.SaveChangesAsync();
        }

        var list = await _client.GetFromJsonAsync<ApiResult<List<WorkflowDto>>>("/api/workflows");

        var emptyRow = list!.Data!.Single(x => x.Id == empty.Data!.Id);
        emptyRow.BpmnStatus.Should().Be("empty");
        emptyRow.BizTypeLabel.Should().Be(emptyRow.BizType);

        var invalidRow = list.Data!.Single(x => x.BizType == "material_transfer");
        invalidRow.BizType.Should().Be("material_transfer");
        invalidRow.BizTypeLabel.Should().Be("测试料件流转");
        invalidRow.BpmnStatus.Should().Be("invalid");
        invalidRow.BpmnValidationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Design_endpoint_updates_only_bpmn_definition()
    {
        await Login();
        var bizType = Unique("designonly");
        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "仅设计流程",
            BizType = bizType
        });

        var saved = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data!.Id}/design",
            new DesignWorkflowRequest { BpmnXml = SimpleBpmn() });

        saved.Data!.Id.Should().Be(created.Data.Id);
        saved.Data.Name.Should().Be(created.Data.Name);
        saved.Data.BizType.Should().Be(created.Data.BizType);
        saved.Data.IsActive.Should().Be(created.Data.IsActive);
        saved.Data.BpmnXml.Should().Be(SimpleBpmn());
    }

    [Theory]
    [InlineData(101, 1, "The field Name must be a string or array type with a maximum length of '100'.")]
    [InlineData(1, 51, "The field BizType must be a string or array type with a maximum length of '50'.")]
    public async Task Workflow_create_rejects_text_longer_than_database_limit(
        int nameLength,
        int bizTypeLength,
        string expectedMessage)
    {
        await Login();

        var response = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = new string('流', nameLength),
            BizType = new string('b', bizTypeLength)
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        result!.Code.Should().Be(4001);
        result.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task Workflow_create_rejects_bpmn_larger_than_mysql_text_limit_in_utf8_bytes()
    {
        await Login();

        var response = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = Unique("超长BPMN"),
            BizType = Unique("large_bpmn"),
            BpmnXml = new string('中', 21_846)
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        result!.Code.Should().Be(4001);
        result.Message.Should().Contain("65535");
    }

    [Fact]
    public void Design_endpoint_requires_design_permission_without_weakening_general_edit()
    {
        var designPermission = typeof(WorkflowController).GetMethod(nameof(WorkflowController.Design))!
            .GetCustomAttribute<HasPermissionAttribute>();
        var editPermission = typeof(WorkflowController).GetMethod(nameof(WorkflowController.Save))!
            .GetCustomAttribute<HasPermissionAttribute>();

        designPermission!.Policy.Should().Be("perm:workflow:design");
        editPermission!.Policy.Should().Be("perm:workflow:edit");
    }

    [Fact]
    public async Task Edit_only_user_cannot_change_bpmn_through_metadata_endpoint()
    {
        await Login();
        var originalBpmn = SimpleBpmn();
        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = Unique("权限隔离流程"),
            BizType = Unique("permission"),
            BpmnXml = originalBpmn
        });
        var employeeNo = Unique("WFEDIT");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = new Role { Code = Unique("wf_edit_role"), Name = Unique("流程编辑角色") };
            var user = new User
            {
                EmployeeNo = employeeNo,
                Name = Unique("流程元数据编辑员"),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123"),
                IsActive = true
            };
            db.AddRange(role, user);
            await db.SaveChangesAsync();
            var editPermission = await db.Permissions.SingleAsync(permission => permission.Code == "workflow:edit");
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = editPermission.Id });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "TestPass123"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data!.Token);

        var changedBpmn = SimpleBpmn().Replace("Review", "ReviewChanged", StringComparison.Ordinal);
        var metadata = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data!.Id}", new
        {
            name = "仅修改元数据",
            bizType = created.Data.BizType,
            bpmnXml = changedBpmn
        });
        var designResponse = await _client.PutAsJsonAsync($"/api/workflows/{created.Data.Id}/design",
            new DesignWorkflowRequest { BpmnXml = changedBpmn });

        metadata.Code.Should().Be(0);
        metadata.Data!.Name.Should().Be("仅修改元数据");
        metadata.Data.BpmnXml.Should().Be(originalBpmn);
        designResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Workflows.SingleAsync(workflow => workflow.Id == created.Data.Id)).BpmnXml
            .Should().Be(originalBpmn);
    }

    [Fact]
    public async Task Create_only_user_can_create_metadata_but_cannot_embed_bpmn_definition()
    {
        await Login();
        var employeeNo = Unique("WFCREATE");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = new Role { Code = Unique("wf_create_role"), Name = Unique("流程创建角色") };
            var user = new User
            {
                EmployeeNo = employeeNo,
                Name = Unique("流程创建员"),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123"),
                IsActive = true
            };
            db.AddRange(role, user);
            await db.SaveChangesAsync();
            var createPermission = await db.Permissions.SingleAsync(permission => permission.Code == "workflow:create");
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = createPermission.Id });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
        var login = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo,
            password = "TestPass123"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data!.Token);

        var metadataOnly = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = Unique("仅创建元数据"),
            BizType = Unique("create_only")
        });
        var embeddedDesignResponse = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = Unique("越权流程图"),
            BizType = Unique("design_bypass"),
            BpmnXml = SimpleBpmn()
        });
        embeddedDesignResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var embeddedDesign = await embeddedDesignResponse.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        metadataOnly.Code.Should().Be(0);
        embeddedDesign!.Code.Should().Be(4030);
        embeddedDesign.Message.Should().Contain("流程设计权限");
    }

    [Fact]
    public async Task Workflow_can_toggle_active_status()
    {
        await Login();
        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "启停测试流程",
            BizType = Unique("toggle")
        });

        var disabled = await Post<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data!.Id}/status", new
        {
            isActive = false
        });
        var enabled = await Post<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data.Id}/status", new
        {
            isActive = true
        });

        disabled.Code.Should().Be(0);
        disabled.Data!.IsActive.Should().BeFalse();
        enabled.Code.Should().Be(0);
        enabled.Data!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Workflow_can_create_same_biz_type_when_existing_one_is_disabled()
    {
        await Login();
        var bizType = Unique("same");
        var first = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "旧流程",
            BizType = bizType
        });
        await Post<ApiResult<WorkflowDto>>($"/api/workflows/{first.Data!.Id}/status", new
        {
            isActive = false
        });

        var second = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "新流程",
            BizType = bizType
        });

        second.Code.Should().Be(0);
        second.Data!.BizType.Should().Be(bizType);
        second.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Workflow_cannot_enable_same_biz_type_when_another_one_is_active()
    {
        await Login();
        var bizType = Unique("active");
        var first = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "启用流程",
            BizType = bizType
        });
        await Post<ApiResult<WorkflowDto>>($"/api/workflows/{first.Data!.Id}/status", new
        {
            isActive = false
        });
        var second = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "另一个启用流程",
            BizType = bizType
        });

        var reEnableFirstResponse = await _client.PostAsJsonAsync($"/api/workflows/{first.Data.Id}/status", new
        {
            isActive = true
        });
        reEnableFirstResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var reEnableFirst = await reEnableFirstResponse.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        second.Code.Should().Be(0);
        reEnableFirst!.Code.Should().Be(4094);
        reEnableFirst.Message.Should().Contain("业务类型已有启用流程");
    }

    [Fact]
    public async Task Database_rejects_duplicate_active_workflow_biz_type()
    {
        var bizType = Unique("dbactive");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Workflows.AddRange(
            new WorkflowEntity { Name = Unique("启用流程A"), BizType = bizType, IsActive = true },
            new WorkflowEntity { Name = Unique("启用流程B"), BizType = bizType, IsActive = true });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Database_allows_duplicate_disabled_workflow_biz_type()
    {
        var bizType = Unique("dbdisabled");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Workflows.AddRange(
            new WorkflowEntity { Name = Unique("停用流程A"), BizType = bizType, IsActive = false },
            new WorkflowEntity { Name = Unique("停用流程B"), BizType = bizType, IsActive = false });

        await db.SaveChangesAsync();

        (await db.Workflows.CountAsync(x => x.BizType == bizType)).Should().Be(2);
    }

    [Fact]
    public async Task Workflow_create_rejects_duplicate_name()
    {
        await Login();
        var name = $"重复名称{Guid.NewGuid():N}"[..18];
        await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = name,
            BizType = Unique("name1")
        });

        var duplicatedResponse = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = name,
            BizType = Unique("name2")
        });
        duplicatedResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicated = await duplicatedResponse.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        duplicated!.Code.Should().Be(4094);
        duplicated.Message.Should().Be("流程名称已存在");
    }

    [Fact]
    public async Task Workflow_update_rejects_duplicate_name()
    {
        await Login();
        var name = $"编辑重复{Guid.NewGuid():N}"[..18];
        var first = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = name,
            BizType = Unique("edit1")
        });
        var second = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = $"待修改{Guid.NewGuid():N}"[..18],
            BizType = Unique("edit2")
        });

        var duplicatedResponse = await _client.PutAsJsonAsync($"/api/workflows/{second.Data!.Id}", new SaveWorkflowRequest
        {
            Name = first.Data!.Name,
            BizType = second.Data.BizType
        });
        duplicatedResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicated = await duplicatedResponse.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        duplicated!.Code.Should().Be(4094);
        duplicated.Message.Should().Be("流程名称已存在");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Workflow_definition_update_creates_new_version_and_preserves_status_when_instance_is_pending(bool isActive)
    {
        await Login();
        var bizType = Unique("versioned");
        var workflowName = Unique("版本化流程");
        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = workflowName,
            BizType = bizType
        });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var asset = await db.Assets.FirstOrDefaultAsync();
            if (asset is null)
            {
                var category = await db.AssetCategories.FirstOrDefaultAsync(x => !x.IsDeleted);
                if (category is null)
                {
                    category = new AssetCategory
                    {
                        CodeSeg = Unique("CAT")[..6],
                        Code = Unique("CATEGORY")
                    };
                    db.AssetCategories.Add(category);
                    await db.SaveChangesAsync();
                }
                asset = new Asset
                {
                    AssetNo = Unique("AST"),
                    Name = "版本测试资产",
                    CategoryId = category.Id,
                    CreatedAt = DateTime.UtcNow
                };
                db.Assets.Add(asset);
                await db.SaveChangesAsync();
            }
            var user = await db.Users.FirstAsync();
            db.ApprovalFlows.Add(new ApprovalFlow
            {
                FlowNo = Unique("APV"),
                BizType = bizType,
                WorkflowId = created.Data!.Id,
                AssetId = asset.Id,
                AssetNo = asset.AssetNo,
                AssetName = asset.Name,
                ApplicantId = user.Id,
                Applicant = user.Name,
                Status = "pending",
                ApplyTime = DateTime.UtcNow,
                Deadline = DateTime.UtcNow.AddDays(1),
                ActiveScopeKey = $"version-test:{asset.Id}:{Guid.NewGuid():N}"
            });
            await db.SaveChangesAsync();
        }

        if (!isActive)
        {
            await Post<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data!.Id}/status", new { isActive = false });
        }

        var saved = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data!.Id}/design",
            new DesignWorkflowRequest { BpmnXml = SimpleBpmn() });

        saved.Code.Should().Be(0, saved.Message);
        saved.Data!.Id.Should().NotBe(created.Data.Id);
        saved.Data.IsActive.Should().Be(isActive);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var oldVersion = await verifyDb.Workflows.SingleAsync(x => x.Id == created.Data.Id);
        oldVersion.IsActive.Should().BeFalse();
        oldVersion.Name.Should().Contain("历史版本");
        (await verifyDb.ApprovalFlows.SingleAsync(x => x.WorkflowId == oldVersion.Id)).Status.Should().Be("pending");
    }

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new
        {
            employeeNo = "1001",
            password = "123456"
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

    private static string SimpleBpmn() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                          xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
          <bpmn:process id="Process_Version" isExecutable="true">
            <bpmn:startEvent id="Start" />
            <bpmn:userTask id="Review" name="审批" camunda:assignee="supervisor" />
            <bpmn:endEvent id="End" />
            <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="Review" />
            <bpmn:sequenceFlow id="Flow_2" sourceRef="Review" targetRef="End" />
          </bpmn:process>
        </bpmn:definitions>
        """;
}
