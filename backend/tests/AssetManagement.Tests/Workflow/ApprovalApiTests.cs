using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Tests.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssetManagement.Tests.Workflow;

/// <summary>
/// 审批流程 API 测试
///
/// 注意：这些测试原本基于旧的 WorkflowNode 模型编写。
/// 在 BPMN 迁移后，需要重写以适配新的架构：
/// - WorkflowDto.Nodes → WorkflowDto.BpmnXml
/// - ApprovalFlowDto.Nodes → ApprovalFlowDto.BpmnTokens
/// - ApprovalFlowDto.CurrentNodeIndex → ApprovalFlowDto.CurrentNodeIds
/// </summary>
public class ApprovalApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public ApprovalApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Workflow_design_can_update_bpmn_xml()
    {
        // 测试：保存有效的 BPMN XML，验证解析正确
        await Login();

        // 创建简单的 BPMN 流程定义
        var simpleBpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn"">
  <bpmn:process id=""testProcess"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" />
    <bpmn:userTask id=""Task_Review"" name=""审核"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""assignee"" value=""1001"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" />
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_Review"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_Review"" targetRef=""EndEvent_1"" />
  </bpmn:process>
</bpmn:definitions>";

        var response = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = "测试BPMN流程",
            BizType = "test-bpmn",
            BpmnXml = simpleBpmn
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();

        result.Should().NotBeNull();
        result!.Code.Should().Be(0);
        result.Data.Should().NotBeNull();
        result.Data!.BpmnXml.Should().Be(simpleBpmn);

        // 验证 BPMN XML 能被正确解析
        var act = () => BpmnParser.Parse(simpleBpmn);
        act.Should().NotThrow("保存的 BPMN XML 应该能被正确解析");
    }

    [Fact]
    public async Task Borrow_flow_creates_pending_flow()
    {
        await Login();
        int adminId;
        int sourceCustodianId;
        int? sourceDepartmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            adminId = await db.Users.Where(user => user.EmployeeNo == "1001").Select(user => user.Id).SingleAsync();
            var sourceCustodian = await db.Users
                .Where(user => user.Id != adminId && user.IsActive)
                .Select(user => new { user.Id, user.DepartmentId })
                .FirstAsync();
            sourceCustodianId = sourceCustodian.Id;
            sourceDepartmentId = sourceCustodian.DepartmentId;
        }
        var asset = await CreateAsset(sourceDepartmentId, sourceCustodianId);

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试借用",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        // 添加响应检查
        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        // 添加 null 检查
        flow.Should().NotBeNull();
        flow!.Code.Should().Be(0, "API 应该返回成功");
        flow.Data.Should().NotBeNull("流程数据不应为空");

        flow.Data!.Status.Should().Be("pending");
        flow.Data.BizType.Should().Be("borrow");
        flow.Data.AssetId.Should().Be(asset.Id);
        // BPMN 模式下，流程应该已经启动并推进到第一个 UserTask
        flow.Data.CurrentNodeIds.Should().NotBeEmpty();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedFlow = await verifyDb.ApprovalFlows.AsNoTracking().SingleAsync(item => item.Id == flow.Data.Id);
        savedFlow.SourceCustodianId.Should().Be(sourceCustodianId,
            "借用发起时必须固化借出前保管人，不能在归还时临时猜测");
    }

    [Fact]
    public async Task Borrow_flow_rejects_current_custodian_as_applicant()
    {
        await Login();
        int adminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            adminId = await db.Users
                .Where(user => user.EmployeeNo == "1001")
                .Select(user => user.Id)
                .SingleAsync();
        }
        var asset = await CreateAsset(null, adminId);

        var result = await PostError<ApprovalFlowDto>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "禁止本人借用本人保管资产",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        }, HttpStatusCode.UnprocessableEntity);

        result.Code.Should().Be(4055);
        result.Message.Should().Be("当前保管人不能借用自己保管的资产");
    }

    [Fact]
    public async Task Return_date_is_stored_as_date_only_but_api_stays_iso_string()
    {
        await Login();
        var asset = await CreateAsset();
        var expectedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(9));
        var expectedText = expectedDate.ToString("yyyy-MM-dd");

        var started = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "日期建模回归",
            ReturnDate = expectedText
        });

        started.Data!.ReturnDate.Should().Be(expectedText);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedDate = await db.ApprovalFlows.AsNoTracking()
            .Where(flow => flow.Id == started.Data.Id)
            .Select(flow => flow.ReturnDate)
            .SingleAsync();
        storedDate.Should().Be(expectedDate);
    }

    [Fact]
    public async Task Approval_page_endpoints_filter_before_count_and_support_flow_id()
    {
        await Login();
        var pageKeyword = Unique("分页批量");
        async Task<ApprovalFlowDto> StartBorrow()
        {
            var asset = await CreateAsset();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var entity = await db.Assets.AsTracking().SingleAsync(item => item.Id == asset.Id);
                entity.Name = pageKeyword;
                await db.SaveChangesAsync();
            }
            var response = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
            {
                BizType = "borrow", AssetId = asset.Id, Reason = "分页筛选",
                ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
            });
            return response.Data!;
        }
        var target = await StartBorrow();
        await StartBorrow();

        var mine = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/mine-page?page=1&pageSize=1&flowId={target.Id}&keyword={target.FlowNo}&bizType=borrow&status=pending");
        var approverNo = target.CurrentSteps.SelectMany(step => step.Assignees).First().EmployeeNo;
        Auth(await LoginToken(approverNo, "123456"));
        var pending = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/pending-page?page=1&pageSize=1&flowId={target.Id}&keyword={target.AssetNo}&bizType=borrow&status=pending");
        var invalidDateResponse = await _client.GetAsync(
            "/api/approvals/pending-return-page?returnDate=2026-2-30");
        invalidDateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalidDate = await invalidDateResponse.Content
            .ReadFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>();

        mine!.Data!.Total.Should().Be(1);
        mine.Data.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
        pending!.Data!.Total.Should().Be(1);
        pending.Data.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
        invalidDate!.Code.Should().Be(4001);
        invalidDate.Message.Should().Contain("日期格式");

        _factory.CommandCounter.Reset();
        var onePending = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/pending-page?page=1&pageSize=1&flowId={target.Id}&keyword={pageKeyword}");
        var onePendingQueries = _factory.CommandCounter.ReaderCount;
        _factory.CommandCounter.Reset();
        var twoPending = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/pending-page?page=1&pageSize=2&keyword={pageKeyword}");
        var twoPendingQueries = _factory.CommandCounter.ReaderCount;
        onePending!.Data!.Total.Should().Be(1);
        twoPending!.Data!.Total.Should().Be(2);
        twoPendingQueries.Should().BeLessThanOrEqualTo(onePendingQueries + 1,
            "待办扫描应跨同模板同上下文流程复用审批人解析，SQL 数不能随流程数线性增长");

        var extremePending = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/pending-page?page={int.MaxValue}&pageSize=100&keyword={pageKeyword}");
        extremePending!.Code.Should().Be(0);
        extremePending.Data!.Total.Should().Be(2);
        extremePending.Data.Items.Should().BeEmpty();

        await Login();
        _factory.CommandCounter.Reset();
        var oneItem = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/mine-page?page=1&pageSize=1&keyword={pageKeyword}");
        var oneItemQueries = _factory.CommandCounter.ReaderCount;
        _factory.CommandCounter.Reset();
        var twoItems = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/mine-page?page=1&pageSize=2&keyword={pageKeyword}");
        var twoItemQueries = _factory.CommandCounter.ReaderCount;
        oneItem!.Data!.Total.Should().Be(2);
        twoItems!.Data!.Items.Should().HaveCount(2);
        twoItemQueries.Should().BeLessThanOrEqualTo(oneItemQueries + 1,
            "相同模板和申请人的流程应复用定义、审批人解析和用户批量查询，SQL 数不能随条目线性增长");

        var extremeMine = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/mine-page?page={int.MaxValue}&pageSize=100&keyword={pageKeyword}");
        extremeMine!.Code.Should().Be(0);
        extremeMine.Data!.Total.Should().Be(2);
        extremeMine.Data.Items.Should().BeEmpty();

        var extremeReturn = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/pending-return-page?page={int.MaxValue}&pageSize=100");
        extremeReturn!.Code.Should().Be(0);
        extremeReturn.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Approver_can_query_flow_after_handling_own_node()
    {
        await Login();
        var asset = await CreateAsset();
        var started = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "审批记录追溯",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });
        var approverNo = started.Data!.CurrentSteps.SelectMany(step => step.Assignees).First().EmployeeNo;
        Auth(await LoginToken(approverNo, "123456"));
        var pending = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/pending-page?page=1&pageSize=20&flowId={started.Data.Id}");
        var actionable = pending!.Data!.Items.Should().ContainSingle().Which;
        var nodeId = actionable.ActionableNodeIds.First();

        var handledBefore = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/handled-page?page=1&pageSize=20&flowId={started.Data.Id}");
        handledBefore!.Data!.Items.Should().BeEmpty();

        var approved = await Post<ApiResult<ApprovalFlowDto>>(
            $"/api/approvals/{started.Data.Id}/approve",
            new ApprovalActionRequest { NodeId = nodeId, Opinion = "同意，保留审批记录" });
        approved.Code.Should().Be(0, approved.Message);

        var handledAfter = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            $"/api/approvals/handled-page?page=1&pageSize=20&flowId={started.Data.Id}");
        var record = handledAfter!.Data!.Items.Should().ContainSingle().Which;
        record.MyApprovalAction.Should().Be("approve");
        record.MyApprovalNodeId.Should().Be(nodeId);
        record.MyApprovalTime.Should().NotBeNull();
        record.ProgressSteps.Should().Contain(step => step.NodeId == nodeId && step.State == "completed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("2026-02-30")]
    [InlineData("2026-7-16")]
    public async Task Borrow_flow_rejects_missing_or_invalid_return_date(string? returnDate)
    {
        await Login();
        var asset = await CreateAsset();

        var result = await PostError<ApprovalFlowDto>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "日期校验回归测试",
            ReturnDate = returnDate
        }, HttpStatusCode.BadRequest);

        result.Code.Should().Be(4001);
        result.Message.Should().Contain("归还日期");
    }

    [Fact]
    public async Task Borrow_flow_rejects_today_and_past_return_dates()
    {
        await Login();
        foreach (var returnDate in new[] { DateTime.Today.AddDays(-1), DateTime.Today })
        {
            var asset = await CreateAsset();
            var result = await PostError<ApprovalFlowDto>("/api/approvals", new StartApprovalRequest
            {
                BizType = "borrow",
                AssetId = asset.Id,
                Reason = "日期校验回归测试",
                ReturnDate = returnDate.ToString("yyyy-MM-dd")
            }, HttpStatusCode.BadRequest);
            result.Code.Should().Be(4001);
            result.Message.Should().Be("归还日期必须晚于今天");
        }
    }

    [Fact]
    public async Task Extension_flow_updates_active_borrow_return_date_only_after_approval()
    {
        await Login();
        var originalReturnDate = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd");
        var newReturnDate = DateTime.Today.AddDays(12).ToString("yyyy-MM-dd");
        var (asset, originalBorrowId) = await CreateBorrowedAsset(originalReturnDate);

        var started = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "extension",
            AssetId = asset.Id,
            Reason = "项目周期延长",
            ReturnDate = newReturnDate
        });

        started.Code.Should().Be(0, started.Message);
        started.Data!.BizType.Should().Be("extension");
        started.Data.OriginalReturnDate.Should().Be(originalReturnDate,
            "延期单必须保留申请时的原期限用于审批和审计");
        started.Data.ReturnDate.Should().Be(newReturnDate);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var beforeApproval = await db.ApprovalFlows.AsNoTracking()
                .SingleAsync(flow => flow.Id == originalBorrowId);
            beforeApproval.ReturnDate.Should().Be(DateOnly.ParseExact(originalReturnDate, "yyyy-MM-dd"),
                "延期审批通过前不能提前修改原借用期限");
        }

        Auth(await LoginToken("TEST-SUPERVISOR", "123456"));
        var approved = await Post<ApiResult<ApprovalFlowDto>>(
            $"/api/approvals/{started.Data.Id}/approve",
            new ApprovalActionRequest
            {
                NodeId = started.Data.CurrentNodeIds.Single(),
                Opinion = "同意延期"
            });

        approved.Code.Should().Be(0, approved.Message);
        approved.Data!.Status.Should().Be("approved");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var afterApproval = await db.ApprovalFlows.AsNoTracking()
                .SingleAsync(flow => flow.Id == originalBorrowId);
            afterApproval.ReturnDate.Should().Be(DateOnly.ParseExact(newReturnDate, "yyyy-MM-dd"),
                "延期审批通过后应更新当前有效的原借用记录");
        }

        await Login();
        var assetResult = await _client.GetFromJsonAsync<ApiResult<AssetDto>>($"/api/assets/{asset.Id}");
        assetResult!.Data!.ReturnDate.Should().Be(newReturnDate,
            "资产查询应立即返回审批后的新归还期限");
    }

    [Fact]
    public async Task Extension_flow_requires_current_custodian_and_a_later_return_date()
    {
        await Login();
        var originalReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
        var (asset, _) = await CreateBorrowedAsset(originalReturnDate);

        var sameDate = await PostError<ApprovalFlowDto>("/api/approvals", new StartApprovalRequest
        {
            BizType = "extension",
            AssetId = asset.Id,
            Reason = "日期没有延后",
            ReturnDate = originalReturnDate
        }, HttpStatusCode.BadRequest);
        sameDate.Code.Should().Be(4001);
        sameDate.Message.Should().Contain("晚于原应归还日期");

        var availableAsset = await CreateAsset();
        var notBorrowed = await PostError<ApprovalFlowDto>("/api/approvals", new StartApprovalRequest
        {
            BizType = "extension",
            AssetId = availableAsset.Id,
            Reason = "在库资产不能延期",
            ReturnDate = DateTime.Today.AddDays(14).ToString("yyyy-MM-dd")
        }, HttpStatusCode.UnprocessableEntity);
        notBorrowed.Code.Should().Be(4055);
        notBorrowed.Message.Should().Contain("当前借用人");
    }

    [Fact]
    public async Task Duplicate_asset_flow_message_identifies_current_applicant_and_flow()
    {
        await Login();
        var asset = await CreateAsset();
        var activeFlow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "占用中的借用申请",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "重复申请",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var duplicated = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        duplicated.Should().NotBeNull();
        duplicated!.Code.Should().Be(4056);
        duplicated.Message.Should().Contain("系统管理员");
        duplicated.Message.Should().Contain("借用申请");
        duplicated.Message.Should().Contain(activeFlow.Data!.FlowNo);
        duplicated.Message.Should().Contain("当前节点");
    }

    [Fact]
    public async Task Borrow_flow_rejects_applicant_without_supervisor()
    {
        await Login();
        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var employeeRole = roles!.Data!.Items.Single(r => r.Code == "employee");
        var employeeNo = Unique("NOSUP");
        var applicant = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = employeeNo,
            Name = Unique("无主管员工"),
            Password = "TestPass123",
            RoleIds = new[] { employeeRole.Id }
        });
        var asset = await CreateAsset();

        Auth(await LoginToken(employeeNo, "TestPass123"));
        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "无主管不应发起",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        result!.Code.Should().Be(4051);
        result.Message.Should().Contain("未配置直属主管");
    }

    [Fact]
    public async Task Disabled_workflow_cannot_start_approval()
    {
        await Login();
        var asset = await CreateAsset();
        var workflow = await Post<ApiResult<WorkflowDto>>("/api/workflows", new SaveWorkflowRequest
        {
            Name = Unique("停用流程"),
            BizType = Unique("disabled"),
            BpmnXml = SimpleBpmn("Disabled_Task")
        });
        await Post<ApiResult<WorkflowDto>>($"/api/workflows/{workflow.Data!.Id}/status", new
        {
            isActive = false
        });

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = workflow.Data.BizType,
            AssetId = asset.Id,
            Reason = "测试停用流程"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        result!.Code.Should().Be(4057);
        result.Message.Should().Contain("流程已停用");
    }

    [Fact]
    public async Task Approve_advances_to_next_node()
    {
        await Login();
        var asset = await CreateAsset();

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试审批",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var flow = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        flow.Should().NotBeNull();
        flow!.Data.Should().NotBeNull();

        var flowId = flow.Data!.Id;
        var initialNodeIds = flow.Data.CurrentNodeIds.ToList();

        Auth(await LoginToken("TEST-SUPERVISOR", "123456"));
        var approveResponse = await _client.PostAsJsonAsync($"/api/approvals/{flowId}/approve",
            new ApprovalActionRequest { Opinion = "同意" });

        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        approved.Should().NotBeNull();
        approved!.Data.Should().NotBeNull();

        // 验证 Token 状态已更新
        approved.Data!.BpmnTokens.Should().NotBeEmpty();

        // 流程应该推进：要么完成，要么到下一个节点
        if (approved.Data.Status == "approved") {
            approved.Data.Status.Should().Be("approved", "默认流程应该完成");
        } else {
            approved.Data.Status.Should().Be("pending");
            approved.Data.CurrentNodeIds.Should().NotBeEmpty("应该有新的活跃节点");
        }
    }

    [Fact]
    public async Task Approval_state_rolls_back_when_notification_write_fails()
    {
        await Login();
        var asset = await CreateAsset();
        var started = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "通知事务回归测试",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });
        var flowId = started.Data!.Id;
        var approverToken = await LoginToken("TEST-SUPERVISOR", "123456");

        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INotificationService>();
                services.AddScoped<INotificationService, FailingNotificationService>();
            }));
        using var failingClient = failingFactory.CreateClient();
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", approverToken);

        var response = await failingClient.PostAsJsonAsync($"/api/approvals/{flowId}/approve",
            new ApprovalActionRequest { Opinion = "同意，但通知写入失败" });
        var body = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        body!.Code.Should().Be(500);
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedFlow = await db.ApprovalFlows.AsNoTracking().SingleAsync(item => item.Id == flowId);
        savedFlow.Status.Should().Be("pending", "通知写入失败时审批状态不能单独提交");
        (await db.FlowRecords.AsNoTracking().AnyAsync(record =>
            record.FlowId == flowId && record.Action == "approve")).Should().BeFalse();
        (await db.Notifications.AsNoTracking().AnyAsync(notification =>
            notification.FlowId == flowId && notification.Type == "approval_approved")).Should().BeFalse();
    }

    [Fact]
    public async Task Reject_terminates_flow()
    {
        await Login();
        var asset = await CreateAsset();

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试驳回",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        flow.Should().NotBeNull();
        flow!.Data.Should().NotBeNull();

        Auth(await LoginToken("TEST-SUPERVISOR", "123456"));
        var rejectResponse = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data!.Id}/reject",
            new RejectRequest { Reason = "不同意" });

        rejectResponse.EnsureSuccessStatusCode();
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        rejected.Should().NotBeNull();
        rejected!.Data.Should().NotBeNull();
        rejected.Data!.Status.Should().Be("rejected");
    }

    [Fact]
    public async Task Concurrent_approve_and_reject_only_commit_one_terminal_action()
    {
        await Login();
        var asset = await CreateAsset();
        var started = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "并发审批回归测试",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"),
        });
        var flowId = started.Data!.Id;
        var token = await LoginToken("TEST-SUPERVISOR", "123456");
        using var approveClient = _factory.CreateClient();
        using var rejectClient = _factory.CreateClient();
        approveClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        rejectClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var approveTask = approveClient.PostAsJsonAsync(
            $"/api/approvals/{flowId}/approve",
            new ApprovalActionRequest { Opinion = "并发通过" });
        var rejectTask = rejectClient.PostAsJsonAsync(
            $"/api/approvals/{flowId}/reject",
            new RejectRequest { Reason = "并发驳回" });
        var responses = await Task.WhenAll(approveTask, rejectTask);

        responses.Count(response => response.IsSuccessStatusCode).Should().Be(1);
        var bodies = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>()));
        bodies.Count(body => body?.Code == 0).Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedFlow = await db.ApprovalFlows.AsNoTracking().SingleAsync(flow => flow.Id == flowId);
        savedFlow.Status.Should().BeOneOf("approved", "rejected");
        var terminalRecordCount = await db.FlowRecords.AsNoTracking().CountAsync(record =>
            record.FlowId == flowId && (record.Action == "approve" || record.Action == "reject"));
        terminalRecordCount.Should().Be(1);
    }

    [Fact]
    public async Task Transfer_receiver_dept_manager_gets_second_node_pending_and_notification()
    {
        await Login();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var supervisorRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");
        var employeeRole = roles.Data.Items.Single(r => r.Code == "employee");
        var deptAdminRole = roles.Data.Items.Single(r => r.Code == "supervisor");

        var sourceDept = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest { Name = Unique("SRC") });
        var targetDept = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest { Name = Unique("DST") });

        var supervisorNo = Unique("SUP");
        var supervisor = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = supervisorNo,
            Name = Unique("主管"),
            Password = "TestPass123",
            DepartmentId = sourceDept.Data!.Id,
            RoleIds = new[] { supervisorRole.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{sourceDept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = sourceDept.Data.Name,
            ManagerId = supervisor.Data!.Id,
            IsActive = true
        });
        var receiverAdminNo = Unique("RDA");
        var receiverAdmin = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = receiverAdminNo,
            Name = Unique("接收管理员"),
            Password = "TestPass123",
            DepartmentId = targetDept.Data!.Id,
            RoleIds = new[] { deptAdminRole.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{targetDept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = targetDept.Data.Name,
            ManagerId = receiverAdmin.Data!.Id,
            IsActive = true
        });

        var applicantNo = Unique("APP");
        var applicant = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = applicantNo,
            Name = Unique("申请人"),
            Password = "TestPass123",
            DepartmentId = sourceDept.Data.Id,
            SupervisorId = supervisor.Data!.Id,
            RoleIds = new[] { employeeRole.Id }
        });
        var receiverNo = Unique("RCV");
        var receiver = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = receiverNo,
            Name = Unique("接收人"),
            Password = "TestPass123",
            DepartmentId = targetDept.Data.Id,
            SupervisorId = receiverAdmin.Data.Id,
            RoleIds = new[] { employeeRole.Id }
        });

        var asset = await CreateAsset(sourceDept.Data.Id, applicant.Data!.Id);
        var expectedReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)).ToString("yyyy-MM-dd");
        var originalBorrowFlowId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var borrowedAsset = await db.Assets.AsTracking().SingleAsync(x => x.Id == asset.Id);
            borrowedAsset.Status = AssetStatus.Borrowed;
            borrowedAsset.RowVersion++;
            var borrowWorkflowId = await db.Workflows
                .Where(x => x.BizType == "borrow" && x.IsActive)
                .Select(x => x.Id)
                .SingleAsync();
            var originalBorrow = new ApprovalFlow
            {
                FlowNo = Unique("BOR"),
                BizType = "borrow",
                WorkflowId = borrowWorkflowId,
                AssetId = asset.Id,
                AssetNo = asset.AssetNo,
                AssetName = asset.Name,
                ApplicantId = applicant.Data.Id,
                Applicant = applicant.Data.Name,
                ApplicantDept = sourceDept.Data.Name,
                ReturnDate = DateOnly.ParseExact(expectedReturnDate, "yyyy-MM-dd"),
                Status = "approved",
                ApplyTime = DateTime.UtcNow.AddDays(-2),
                Deadline = DateTime.UtcNow.AddDays(1)
            };
            db.ApprovalFlows.Add(originalBorrow);
            await db.SaveChangesAsync();
            originalBorrowFlowId = originalBorrow.Id;
        }

        Auth(await LoginToken(applicantNo, "TestPass123"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "transfer",
            AssetId = asset.Id,
            TransfereeId = receiver.Data!.Id,
            Reason = "转让到接收部门"
        });
        flow.Code.Should().Be(0, flow.Message);
        flow.Data!.ReturnDate.Should().Be(expectedReturnDate,
            "借出资产转让时必须继承原借用申请的应归还日期");

        Auth(await LoginToken(supervisorNo, "TestPass123"));
        var step1 = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { NodeId = "Task_supervisorRole", Opinion = "同意" });
        step1.Data!.CurrentNodeIds.Should().Contain("Task_receiver");

        Auth(await LoginToken(receiverAdminNo, "TestPass123"));
        var pending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>("/api/approvals/pending");
        pending!.Data.Should().Contain(x => x.Id == flow.Data.Id,
            "转让第二节点的 deptManager 应按接收人部门解析，而不是申请人部门");

        var notifications = await _client.GetFromJsonAsync<ApiResult<List<NotificationDto>>>("/api/notifications");
        notifications!.Data.Should().Contain(x => x.Type == "approval_pending" && x.FlowId == flow.Data.Id);

        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve",
            new ApprovalActionRequest { NodeId = "Task_receiver", Opinion = "同意" });
        approved.Data!.Status.Should().Be("approved");

        var transferredAsset = await _client.GetFromJsonAsync<ApiResult<AssetDto>>($"/api/assets/{asset.Id}");
        transferredAsset!.Data!.Status.Should().Be(AssetStatus.Borrowed,
            "借出资产转让后仍应保持借出状态");
        transferredAsset.Data.CustodianId.Should().Be(receiver.Data.Id);
        transferredAsset.Data.ReturnDate.Should().Be(expectedReturnDate);

        var pendingReturns = await _client.GetFromJsonAsync<ApiResult<PagedResult<ApprovalFlowDto>>>(
            "/api/approvals/pending-return-page?page=1&pageSize=20");
        var transferredBorrow = pendingReturns!.Data!.Items.Single(x => x.Id == originalBorrowFlowId);
        transferredBorrow.Applicant.Should().Be(receiver.Data.Name,
            "待确认归还列表必须显示转让后的当前借用人");
        transferredBorrow.ReturnDate.Should().Be(expectedReturnDate);

        Auth(await LoginToken(receiverNo, "TestPass123"));
        var receiverNotifications = await _client.GetFromJsonAsync<ApiResult<List<NotificationDto>>>("/api/notifications");
        receiverNotifications!.Data.Should().Contain(x =>
            x.Type == "transfer_received"
            && x.FlowId == flow.Data.Id
            && x.Title.Contains(flow.Data.AssetName),
            "资产转让全部审批通过后应通知接收人");
    }

    [Fact]
    public async Task Transfer_flow_rejects_non_custodian_applicant()
    {
        await Login();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var employeeRole = roles!.Data!.Items.Single(r => r.Code == "employee");
        var custodian = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("CST"),
            Name = Unique("保管人"),
            Password = "TestPass123",
            RoleIds = new[] { employeeRole.Id }
        });
        var receiver = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = Unique("RCV"),
            Name = Unique("接收人"),
            Password = "TestPass123",
            RoleIds = new[] { employeeRole.Id }
        });
        var asset = await CreateAsset(null, custodian.Data!.Id);

        var response = await _client.PostAsJsonAsync("/api/approvals", new StartApprovalRequest
        {
            BizType = "transfer",
            AssetId = asset.Id,
            TransfereeId = receiver.Data!.Id,
            Reason = "非保管人尝试转让"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        result.Should().NotBeNull();
        result!.Code.Should().Be(4055);
        result.Message.Should().Contain("只有当前保管人");
    }

    [Fact]
    public async Task Applicant_can_withdraw_pending_flow_and_release_asset_lock()
    {
        await Login();
        var asset = await CreateAsset();
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "稍后撤回",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var employeeRole = roles!.Data!.Items.Single(r => r.Code == "employee");
        var otherEmployeeNo = Unique("OTH");
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = otherEmployeeNo,
            Name = Unique("其他员工"),
            Password = "TestPass123",
            RoleIds = new[] { employeeRole.Id }
        });

        Auth(await LoginToken(otherEmployeeNo, "TestPass123"));
        var forbiddenResponse = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data!.Id}/withdraw", new { });
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var forbidden = await forbiddenResponse.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        forbidden!.Code.Should().Be(4031);
        forbidden.Message.Should().Contain("申请人本人");

        Auth(await LoginToken("1001", "123456"));
        var withdrawn = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/withdraw", new { });
        withdrawn.Data!.Status.Should().Be("withdrawn");
        withdrawn.Data.CurrentNodeIds.Should().BeEmpty();

        var detail = await _client.GetFromJsonAsync<ApiResult<AssetDetailDto>>($"/api/assets/{asset.Id}/detail");
        detail!.Data!.Flows.Should().ContainSingle(x =>
            x.Id == flow.Data.Id && x.Status == "withdrawn" && x.WithdrawnAt.HasValue);

        var replacement = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "撤回后重新发起",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });
        replacement.Code.Should().Be(0, "撤回后应释放资产的进行中流程锁");
    }

    [Fact]
    public async Task Supervisor_node_resolves_department_manager_without_user_supervisor()
    {
        await Login();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var supervisorRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");
        var employeeRole = roles.Data.Items.Single(r => r.Code == "employee");

        var dept = await Post<ApiResult<DepartmentNodeDto>>("/api/departments", new CreateDepartmentRequest { Name = Unique("课别") });
        var managerNo = Unique("MGR");
        var manager = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = managerNo,
            Name = Unique("课级主管"),
            Password = "TestPass123",
            DepartmentId = dept.Data!.Id,
            RoleIds = new[] { supervisorRole.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{dept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = dept.Data.Name,
            ManagerId = manager.Data!.Id,
            IsActive = true
        });

        var applicantNo = Unique("APP");
        var applicant = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = applicantNo,
            Name = Unique("申请人"),
            Password = "TestPass123",
            DepartmentId = dept.Data.Id,
            RoleIds = new[] { employeeRole.Id }
        });

        var asset = await CreateAsset(dept.Data.Id, manager.Data!.Id);
        Auth(await LoginToken(applicantNo, "TestPass123"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "按组织负责人审批",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });

        Auth(await LoginToken(managerNo, "TestPass123"));
        var pending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>("/api/approvals/pending");
        pending!.Data.Should().Contain(x => x.Id == flow.Data!.Id,
            "直属主管节点应优先按申请人所属组织节点负责人解析");

        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { Opinion = "同意" });
        approved.Code.Should().Be(0);
    }

    [Fact]
    public async Task Only_asset_organization_manager_can_confirm_borrow_return()
    {
        await Login();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var supervisorRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");
        var employeeRole = roles.Data.Items.Single(r => r.Code == "employee");
        var dept = await Post<ApiResult<DepartmentNodeDto>>("/api/departments",
            new CreateDepartmentRequest { Name = Unique("归还课别") });
        var managerNo = Unique("RETURN-MGR");
        var manager = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = managerNo,
            Name = Unique("归还负责人"),
            Password = "TestPass123",
            DepartmentId = dept.Data!.Id,
            RoleIds = new[] { supervisorRole.Id }
        });
        await Put<ApiResult<DepartmentNodeDto>>($"/api/departments/{dept.Data.Id}", new UpdateDepartmentRequest
        {
            Name = dept.Data.Name,
            ManagerId = manager.Data!.Id,
            IsActive = true
        });
        var applicantNo = Unique("RETURN-APP");
        var applicant = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = applicantNo,
            Name = Unique("归还申请人"),
            Password = "TestPass123",
            DepartmentId = dept.Data.Id,
            RoleIds = new[] { employeeRole.Id }
        });
        var asset = await CreateAsset(dept.Data.Id, null);

        Auth(await LoginToken(applicantNo, "TestPass123"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "归还权限测试",
            ReturnDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
        });
        Auth(await LoginToken(managerNo, "TestPass123"));
        var approved = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { Opinion = "同意" });
        approved.Data!.Status.Should().Be("approved");

        Auth(await LoginToken("1001", "123456"));
        var adminPending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>(
            "/api/approvals/pending-return");
        adminPending!.Data.Should().NotContain(x => x.Id == flow.Data.Id,
            "系统管理员没有管理该资产所属组织，不能代替业务负责人确认归还");
        var denied = await PostError<ApprovalFlowDto>(
            $"/api/approvals/{flow.Data.Id}/confirm-return",
            new { },
            HttpStatusCode.Forbidden);
        denied.Code.Should().Be(4030);
        denied.Message.Should().Contain("资产所属组织负责人");

        Auth(await LoginToken(managerNo, "TestPass123"));
        var managerPending = await _client.GetFromJsonAsync<ApiResult<List<ApprovalFlowDto>>>(
            "/api/approvals/pending-return");
        managerPending!.Data.Should().Contain(x => x.Id == flow.Data.Id);
        var confirmed = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/confirm-return", new { });
        confirmed.Code.Should().Be(0);
        confirmed.Data!.ConfirmedAt.Should().NotBeNull();

        var returnedAsset = await _client.GetFromJsonAsync<ApiResult<AssetDto>>($"/api/assets/{asset.Id}");
        returnedAsset!.Data!.Status.Should().Be(AssetStatus.Available);
        returnedAsset.Data.CustodianId.Should().Be(manager.Data.Id,
            "借出前没有保管人时，应由实际确认入库的资产所属组织负责人接管");
    }

    [Fact]
    public async Task Exclusive_gateway_routes_based_on_condition()
    {
        // 测试 BPMN ExclusiveGateway 根据条件选择不同分支
        await Login();

        // 创建包含排他网关的 BPMN 流程
        var conditionalBpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn"">
  <bpmn:process id=""conditionalProcess"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:exclusiveGateway id=""Gateway_Dept"" />
    <bpmn:userTask id=""Task_TechDept"" name=""技术部审批"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""assignee"" value=""user:1"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_AdminDept"" name=""行政部审批"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""assignee"" value=""user:1"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id=""End"" />
    <bpmn:sequenceFlow id=""Flow_Start"" sourceRef=""Start"" targetRef=""Gateway_Dept"" />
    <bpmn:sequenceFlow id=""Flow_Tech"" sourceRef=""Gateway_Dept"" targetRef=""Task_TechDept"">
      <bpmn:conditionExpression>${applicantDept} == &quot;技术部&quot;</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_Admin"" sourceRef=""Gateway_Dept"" targetRef=""Task_AdminDept"" />
    <bpmn:sequenceFlow id=""Flow_TechEnd"" sourceRef=""Task_TechDept"" targetRef=""End"" />
    <bpmn:sequenceFlow id=""Flow_AdminEnd"" sourceRef=""Task_AdminDept"" targetRef=""End"" />
  </bpmn:process>
</bpmn:definitions>";

        // 保存流程
        var saveResponse = await _client.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest
        {
            Name = "条件分支测试流程",
            BizType = "test-condition",
            BpmnXml = conditionalBpmn
        });
        var saveResult = await saveResponse.Content.ReadFromJsonAsync<ApiResult<WorkflowDto>>();
        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK, saveResult?.Message);
        saveResult!.Code.Should().Be(0, saveResult.Message);

        // 验证 BPMN 解析成功
        var act = () => BpmnParser.Parse(conditionalBpmn);
        act.Should().NotThrow("包含排他网关的 BPMN 应该能正确解析");

        var process = BpmnParser.Parse(conditionalBpmn);
        process.Nodes.Should().Contain(n => n.Type == BpmnNodeType.ExclusiveGateway);

        // 验证网关有两个出边：一个条件分支，一个无条件默认分支
        var gateway = process.Nodes.First(n => n.Type == BpmnNodeType.ExclusiveGateway);
        var outgoingFlows = process.GetOutgoingFlows(gateway.Id);
        outgoingFlows.Should().HaveCount(2);
        outgoingFlows.Should().ContainSingle(f => !string.IsNullOrEmpty(f.ConditionExpression));
        outgoingFlows.Should().ContainSingle(f => string.IsNullOrEmpty(f.ConditionExpression));
    }

    private async Task Login()
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo = "1001", password = "123456" });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.Token);
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo, password });
        return body.Data!.Token;
    }

    private async Task<AssetDto> CreateAsset()
        => await CreateAsset(null, null);

    private async Task<AssetDto> CreateAsset(int? departmentId, int? custodianId)
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            CodeSeg = UniqueCodeSeg()
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            CodeSeg = UniqueCodeSeg()
        });
        var asset = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "测试资产",
            CategoryId = child.Data!.Id,
            DepartmentId = departmentId,
            CustodianId = custodianId
        });
        return asset.Data!;
    }

    private async Task<(AssetDto Asset, int BorrowFlowId)> CreateBorrowedAsset(string returnDate)
    {
        var asset = await CreateAsset();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.Users.AsNoTracking().SingleAsync(user => user.EmployeeNo == "1001");
        var borrowedAsset = await db.Assets.AsTracking().SingleAsync(item => item.Id == asset.Id);
        borrowedAsset.Status = AssetStatus.Borrowed;
        borrowedAsset.CustodianId = admin.Id;
        borrowedAsset.RowVersion++;
        var borrowWorkflowId = await db.Workflows.AsNoTracking()
            .Where(workflow => workflow.BizType == "borrow" && workflow.IsActive)
            .Select(workflow => workflow.Id)
            .SingleAsync();
        var borrowFlow = new ApprovalFlow
        {
            FlowNo = Unique("BOR"),
            BizType = "borrow",
            WorkflowId = borrowWorkflowId,
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = admin.Id,
            Applicant = admin.Name,
            ReturnDate = DateOnly.ParseExact(returnDate, "yyyy-MM-dd"),
            Status = "approved",
            ApplyTime = DateTime.UtcNow.AddDays(-1),
            Deadline = DateTime.UtcNow.AddDays(1)
        };
        db.ApprovalFlows.Add(borrowFlow);
        await db.SaveChangesAsync();
        return (asset, borrowFlow.Id);
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<ApiResult<T>> PostError<T>(string url, object body, HttpStatusCode expectedStatus)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(expectedStatus);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!;
    }

    private async Task<T> Put<T>(string url, object body)
    {
        var res = await _client.PutAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 50)];

    private static string UniqueCodeSeg()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static string SimpleBpmn(string taskId) => $$"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="Process_Simple" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:userTask id="{{taskId}}" name="审批" camunda:assignee="系统管理员" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="Start" targetRef="{{taskId}}" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="{{taskId}}" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";
}
