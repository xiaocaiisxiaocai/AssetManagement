using System.Net.Http.Headers;
using System.Net.Http.Json;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetManagement.Tests.Workflow;

public class ApprovalApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public ApprovalApiTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Workflow_design_can_update_nodes()
    {
        await Login();
        var workflows = await _client.GetFromJsonAsync<ApiResult<List<WorkflowDto>>>("/api/workflows");
        var workflow = workflows!.Data!.Single(x => x.BizType == "return");
        var nodes = workflow.Nodes.ToList();
        nodes.Insert(nodes.Count - 1, new WorkflowNode
        {
            Id = Unique("extra"),
            Name = "临时复核",
            Type = NodeType.Approval,
            ApproverType = ApproverType.User,
            Approver = "管理员"
        });

        var updated = await Put<ApiResult<WorkflowDto>>($"/api/workflows/{workflow.Id}", new SaveWorkflowRequest
        {
            Name = workflow.Name,
            BizType = workflow.BizType,
            Nodes = nodes
        });

        updated.Data!.Nodes.Should().HaveCount(workflow.Nodes.Count + 1);
    }

    [Fact]
    public async Task Borrow_approval_flow_finishes_and_marks_asset_borrowed()
    {
        await Login();
        await ResetBorrowWorkflow();
        var asset = await CreateAsset(price: 3500);

        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "项目测试",
            ReturnDate = "2026-06-20"
        });

        flow.Data!.CurrentNodeIndex.Should().BeGreaterThan(0);
        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve", new ApprovalActionRequest
        {
            Opinion = "同意"
        });
        await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve", new ApprovalActionRequest
        {
            Signer = "张三",
            Opinion = "ok"
        });
        var finished = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data.Id}/approve", new ApprovalActionRequest
        {
            Signer = "赵敏",
            Opinion = "ok"
        });
        var assetAfter = await _client.GetFromJsonAsync<ApiResult<AssetDto>>($"/api/assets/{asset.Id}");

        finished.Data!.Status.Should().Be("approved");
        assetAfter!.Data!.Status.Should().Be(AssetStatus.Borrowed);
    }

    [Fact]
    public async Task Reject_flow_does_not_change_asset()
    {
        await Login();
        await ResetBorrowWorkflow();
        var asset = await CreateAsset(price: 1200);
        var flow = await Post<ApiResult<ApprovalFlowDto>>("/api/approvals", new StartApprovalRequest
        {
            BizType = "borrow",
            AssetId = asset.Id,
            Reason = "需要借用"
        });

        var rejected = await Post<ApiResult<ApprovalFlowDto>>($"/api/approvals/{flow.Data!.Id}/reject", new RejectRequest
        {
            Reason = "资料不全"
        });
        var assetAfter = await _client.GetFromJsonAsync<ApiResult<AssetDto>>($"/api/assets/{asset.Id}");

        rejected.Data!.Status.Should().Be("rejected");
        assetAfter!.Data!.Status.Should().Be(AssetStatus.Available);
    }

    private async Task<AssetDto> CreateAsset(decimal price)
    {
        var root = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            Name = "流程分类",
            CodeSeg = Unique("WF")
        });
        var child = await Post<ApiResult<CategoryNodeDto>>("/api/categories", new CreateCategoryRequest
        {
            ParentId = root.Data!.Id,
            Name = "流程末级",
            CodeSeg = Unique("LEAF")
        });
        var asset = await Post<ApiResult<AssetDto>>("/api/assets", new CreateAssetRequest
        {
            Name = "审批测试资产",
            CategoryId = child.Data!.Id,
            Price = price
        });
        return asset.Data!;
    }

    private async Task ResetBorrowWorkflow()
    {
        var workflows = await _client.GetFromJsonAsync<ApiResult<List<WorkflowDto>>>("/api/workflows");
        var workflow = workflows!.Data!.Single(x => x.BizType == "borrow");
        await Put<ApiResult<WorkflowDto>>($"/api/workflows/{workflow.Id}", new SaveWorkflowRequest
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
        => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, prefix.Length + 33)];
}
