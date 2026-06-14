using System.Text.RegularExpressions;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Domain.Workflow;

public static class WorkflowEngine
{
    public static FlowInstanceNode Current(ApprovalFlow flow)
        => flow.Nodes[flow.CurrentNodeIndex];

    public static bool IsFinished(ApprovalFlow flow)
        => flow.Status == "approved";

    public static void Start(ApprovalFlow flow)
    {
        flow.CurrentNodeIndex = 0;
        Advance(flow);
    }

    public static void Approve(ApprovalFlow flow, string? signer, string opinion)
    {
        var node = Current(flow);
        if (node.Type is NodeType.Countersign or NodeType.Orsign)
        {
            node.SignStates ??= new Dictionary<string, bool>();
            var who = signer ?? node.Approver ?? "";
            if (!string.IsNullOrWhiteSpace(who))
            {
                node.SignStates[who] = true;
            }

            var signers = (node.Signers ?? new()).Concat(node.AddedSigners ?? new()).Distinct().ToList();
            if (node.Type == NodeType.Countersign && signers.Any(x => !node.SignStates.GetValueOrDefault(x)))
            {
                return;
            }
        }

        node.Status = NodeStatus.Done;
        node.Opinion = opinion;
        node.Time = DateTime.Now.ToString("MM-dd HH:mm");
        Advance(flow);
    }

    public static void Reject(ApprovalFlow flow, string reason)
    {
        var node = Current(flow);
        node.Status = NodeStatus.Rejected;
        node.Opinion = reason;
        node.Time = DateTime.Now.ToString("MM-dd HH:mm");
        flow.Status = "rejected";
    }

    public static void AddSign(ApprovalFlow flow, string who)
    {
        var node = Current(flow);
        node.AddedSigners ??= new List<string>();
        if (!node.AddedSigners.Contains(who))
        {
            node.AddedSigners.Add(who);
        }

        if (node.Type is NodeType.Countersign or NodeType.Orsign)
        {
            node.Signers ??= new List<string>();
            if (!node.Signers.Contains(who))
            {
                node.Signers.Add(who);
            }

            node.SignStates ??= new Dictionary<string, bool>();
            node.SignStates.TryAdd(who, false);
        }
    }

    public static void Transfer(ApprovalFlow flow, string who)
        => Current(flow).Approver = who;

    private static void Advance(ApprovalFlow flow)
    {
        for (var i = flow.CurrentNodeIndex + 1; i < flow.Nodes.Count; i++)
        {
            var next = flow.Nodes[i];
            if (next.Type == NodeType.End)
            {
                next.Status = NodeStatus.Done;
                flow.CurrentNodeIndex = i;
                flow.Status = "approved";
                return;
            }

            if (next.Type == NodeType.Condition && !EvalCondition(next.Condition, flow))
            {
                next.Status = NodeStatus.Skipped;
                continue;
            }

            if (next.Type == NodeType.Cc)
            {
                next.Status = NodeStatus.Done;
                next.Opinion = "已知会";
                continue;
            }

            next.Status = NodeStatus.Current;
            flow.CurrentNodeIndex = i;
            return;
        }

        flow.Status = "approved";
    }

    public static bool EvalCondition(string? condition, ApprovalFlow flow)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var match = Regex.Match(condition, @"amount\s*>\s*(\d+)");
        return !match.Success || flow.Amount > decimal.Parse(match.Groups[1].Value);
    }
}
