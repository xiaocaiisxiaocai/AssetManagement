using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

/// <summary>
/// 审批安全相关回归测试:越权处理、终态保护。
/// </summary>
public class ApprovalSecurityTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public ApprovalSecurityTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // P0-2:非当前节点审批人(且非超级管理员)不得处理他人工单
    [Fact]
    public async Task NonApprover_cannot_approve_others_flow()
    {
        Auth(await LoginToken("1001", "123456"));
        await ResetBorrowWorkflow();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var supervisorRole = roles!.Data!.Items.Single(r => r.Code == "supervisor");

        var empNo = $"SUP{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}";
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = empNo,
            Name = "独立主管",
            Password = "123456",
            RoleIds = new[] { supervisorRole.Id }
        });

        var asset = await CreateAsset(1500);
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试越权"
        });

        // 以非审批人(独立主管)登录,尝试处理该工单
        Auth(await LoginToken(empNo, "123456"));
        var res = await _client.PostAsJsonAsync($"/api/approvals/{flow.Data!.Id}/approve",
            new ApprovalActionRequest { Opinion = "越权同意" });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();

        body!.Code.Should().NotBe(0, "非当前节点审批人不应能处理他人工单");
    }

    // P0-5:已通过(终态)的流程不得再被驳回翻盘
    [Fact]
    public async Task Approved_flow_cannot_be_rejected()
    {
        Auth(await LoginToken("1001", "123456"));
        await ResetBorrowWorkflow();

        var asset = await CreateAsset(1500);
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试终态"
        });
        var id = flow.Data!.Id;

        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{id}/approve", new ApprovalActionRequest { Opinion = "同意" });
        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{id}/approve", new ApprovalActionRequest { Signer = "张三", Opinion = "ok" });
        var done = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{id}/approve", new ApprovalActionRequest { Signer = "赵敏", Opinion = "ok" });
        done.Data!.Status.Should().Be("approved");

        var res = await _client.PostAsJsonAsync($"/api/approvals/{id}/reject", new RejectRequest { Reason = "想翻盘" });
        var body = await res.Content.ReadFromJsonAsync<ApiResult<ApprovalFlowDto>>();
        body!.Code.Should().NotBe(0, "已结束流程不应允许驳回");

        var after = await _client.GetFromJsonAsync<ApiResult<ApprovalFlowDto>>($"/api/approvals/{id}");
        after!.Data!.Status.Should().Be("approved", "驳回应被拒绝,状态保持通过");
    }

    // P1-6:直属主管节点应解析为申请人的实际上级,而非模板配置的占位姓名
    [Fact]
    public async Task Supervisor_node_resolves_to_applicant_manager()
    {
        Auth(await LoginToken("1001", "123456"));
        await ResetBorrowWorkflow();

        var roles = await _client.GetFromJsonAsync<ApiResult<PagedResult<RoleDto>>>("/api/roles");
        var empRole = roles!.Data!.Items.Single(r => r.Code == "employee");

        var supNo = $"MG{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}";
        var sup = await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = supNo,
            Name = "王上级",
            Password = "123456",
            RoleIds = new[] { empRole.Id },
        });
        var appNo = $"EM{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}";
        await Post<ApiResult<UserDto>>("/api/users", new CreateUserRequest
        {
            EmployeeNo = appNo,
            Name = "小员工",
            Password = "123456",
            SupervisorId = sup.Data!.Id,
            RoleIds = new[] { empRole.Id },
        });

        var asset = await CreateAsset(1500);

        // 以有上级的申请人身份发起借用流程
        Auth(await LoginToken(appNo, "123456"));
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "测试上级解析",
        });

        // 第二个节点(直属主管审批,ApproverType=Supervisor)应解析为申请人上级
        flow.Data!.Nodes[1].Approver.Should().Be("王上级");
    }

    private async Task<AssetDto> CreateAsset(decimal price)
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            Name = "安全分类",
            CodeSeg = Unique("SEC")
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            Name = "安全末级",
            CodeSeg = Unique("LEAF")
        });
        var asset = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "安全测试资产",
            CategoryId = child.Data!.Id,
            Price = price
        });
        return asset.Data!;
    }

    private async Task ResetBorrowWorkflow()
    {
        var workflows = await _client.GetFromJsonAsync<ApiResult<List<WorkflowDto>>>("/api/workflows");
        var workflow = workflows!.Data!.Single(x => x.BizType == "borrow");
        await _client.PutAsJsonAsync($"/api/workflows/{workflow.Id}", new SaveWorkflowRequest
        {
            Name = workflow.Name,
            BizType = workflow.BizType,
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "b1", Name = "发起", Type = NodeType.Start },
                new() { Id = "b2", Name = "直属主管审批", Type = NodeType.Approval, ApproverType = ApproverType.Supervisor, Approver = "李主管" },
                new() { Id = "b3", Name = "资产管理员会签", Type = NodeType.Countersign, ApproverType = ApproverType.Role, Approver = "资产管理员", Signers = new List<string> { "张三", "赵敏" } },
                new() { Id = "b4", Name = "分管副总审批", Type = NodeType.Condition, ApproverType = ApproverType.User, Approver = "王副总", Condition = "amount>5000" },
                new() { Id = "b5", Name = "结束", Type = NodeType.End }
            }
        });
    }

    private void Auth(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> LoginToken(string employeeNo, string password)
    {
        var body = await Post<ApiResult<LoginResponse>>("/api/auth/login", new { employeeNo, password });
        return body.Data!.Token;
    }

    private async Task<T> Post<T>(string url, object body)
    {
        var res = await _client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private static string Unique(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];
}
