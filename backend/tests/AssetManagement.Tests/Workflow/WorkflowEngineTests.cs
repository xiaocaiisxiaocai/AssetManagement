using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

public class WorkflowEngineTests
{
    [Fact]
    public void Approve_single_advances()
    {
        var flow = Flow(N("发起", NodeStatus.Done), N("主管", NodeStatus.Current, NodeType.Approval), N("结束", NodeStatus.Pending, NodeType.End));

        WorkflowEngine.Approve(flow, signer: null, opinion: "同意");

        flow.Nodes[1].Status.Should().Be(NodeStatus.Done);
        flow.CurrentNodeIndex.Should().Be(2);
        WorkflowEngine.IsFinished(flow).Should().BeTrue();
    }

    [Fact]
    public void Countersign_waits_until_all()
    {
        var flow = Flow(N("发起", NodeStatus.Done), Sign("会签", NodeType.Countersign, "张三", "赵敏"), N("结束", NodeStatus.Pending, NodeType.End));

        WorkflowEngine.Approve(flow, "张三", "ok");
        flow.CurrentNodeIndex.Should().Be(1);

        WorkflowEngine.Approve(flow, "赵敏", "ok");
        flow.CurrentNodeIndex.Should().Be(2);
    }

    [Fact]
    public void Orsign_advances_on_first()
    {
        var flow = Flow(N("发起", NodeStatus.Done), Sign("或签", NodeType.Orsign, "张三", "赵敏"), N("结束", NodeStatus.Pending, NodeType.End));

        WorkflowEngine.Approve(flow, "张三", "ok");

        flow.CurrentNodeIndex.Should().Be(2);
    }

    [Fact]
    public void Condition_skipped_when_not_met()
    {
        var flow = Flow(
            N("发起", NodeStatus.Done),
            N("主管", NodeStatus.Current, NodeType.Approval),
            Cond("副总", "amount>5000"),
            N("结束", NodeStatus.Pending, NodeType.End));
        flow.Amount = 3500;

        WorkflowEngine.Approve(flow, null, "ok");

        flow.Nodes[2].Status.Should().Be(NodeStatus.Skipped);
        WorkflowEngine.IsFinished(flow).Should().BeTrue();
    }

    [Fact]
    public void Condition_kept_when_met()
    {
        var flow = Flow(
            N("发起", NodeStatus.Done),
            N("主管", NodeStatus.Current, NodeType.Approval),
            Cond("副总", "amount>5000"),
            N("结束", NodeStatus.Pending, NodeType.End));
        flow.Amount = 8000;

        WorkflowEngine.Approve(flow, null, "ok");

        flow.CurrentNodeIndex.Should().Be(2);
        flow.Nodes[2].Status.Should().Be(NodeStatus.Current);
    }

    [Fact]
    public void Reject_terminates()
    {
        var flow = Flow(N("发起", NodeStatus.Done), N("主管", NodeStatus.Current, NodeType.Approval), N("结束", NodeStatus.Pending, NodeType.End));

        WorkflowEngine.Reject(flow, "理由");

        flow.Status.Should().Be("rejected");
    }

    [Fact]
    public void AddSign_adds_pending_signer()
    {
        var flow = Flow(N("发起", NodeStatus.Done), Sign("会签", NodeType.Countersign, "张三"), N("结束", NodeStatus.Pending, NodeType.End));

        WorkflowEngine.AddSign(flow, "王五");

        flow.Nodes[1].Signers.Should().Contain("王五");
    }

    private static ApprovalFlow Flow(params FlowInstanceNode[] nodes) => new()
    {
        CurrentNodeIndex = Array.FindIndex(nodes, x => x.Status == NodeStatus.Current),
        Nodes = nodes.ToList(),
        Status = "pending"
    };

    private static FlowInstanceNode N(string name, NodeStatus status, NodeType type = NodeType.Start) => new()
    {
        Name = name,
        Status = status,
        Type = type
    };

    private static FlowInstanceNode Sign(string name, NodeType type, params string[] signers) => new()
    {
        Name = name,
        Type = type,
        Status = NodeStatus.Current,
        Signers = signers.ToList(),
        SignStates = signers.ToDictionary(x => x, _ => false)
    };

    private static FlowInstanceNode Cond(string name, string condition) => new()
    {
        Name = name,
        Type = NodeType.Condition,
        Status = NodeStatus.Pending,
        Condition = condition
    };
}
