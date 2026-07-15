using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        var getDeleted = await _client.GetFromJsonAsync<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data.Id}");
        getDeleted!.Code.Should().Be(4049);
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

        var reEnableFirst = await Post<ApiResult<WorkflowDto>>($"/api/workflows/{first.Data.Id}/status", new
        {
            isActive = true
        });

        second.Code.Should().Be(0);
        reEnableFirst.Code.Should().Be(4094);
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

        var duplicated = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = name,
            BizType = Unique("name2")
        });

        duplicated.Code.Should().Be(4094);
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

        var duplicated = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{second.Data!.Id}", new SaveWorkflowRequest
        {
            Name = first.Data!.Name,
            BizType = second.Data.BizType
        });

        duplicated.Code.Should().Be(4094);
        duplicated.Message.Should().Be("流程名称已存在");
    }

    [Fact]
    public async Task Workflow_definition_update_creates_new_version_when_instance_is_pending()
    {
        await Login();
        var bizType = Unique("versioned");
        var created = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = "版本化流程",
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

        var saved = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{created.Data!.Id}", new SaveWorkflowRequest
        {
            Name = "版本化流程",
            BizType = bizType,
            BpmnXml = SimpleBpmn()
        });

        saved.Code.Should().Be(0, saved.Message);
        saved.Data!.Id.Should().NotBe(created.Data.Id);
        saved.Data.IsActive.Should().BeTrue();
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
