using System.Globalization;
using System.Text.RegularExpressions;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Domain.Workflow;

public static class WorkflowEngine
{
    public static FlowInstanceNode Current(ApprovalFlow flow)
        => flow.Nodes[flow.CurrentNodeIndex];

    public static bool IsFinished(ApprovalFlow flow)
        => flow.Status == "approved";

    // 终态纵深防御:已通过/已驳回的流程不能再推进或驳回(编排层已先行校验,此处防止直接误用)
    private static void EnsureNotFinished(ApprovalFlow flow)
    {
        if (flow.Status is "approved" or "rejected")
        {
            throw new InvalidOperationException("流程已结束,不能继续处理");
        }
    }

    public static void Start(ApprovalFlow flow)
    {
        flow.CurrentNodeIndex = 0;
        Advance(flow);
    }

    public static void Approve(ApprovalFlow flow, string? signer, string opinion)
    {
        EnsureNotFinished(flow);
        var node = Current(flow);
        if (node.Type is NodeType.Countersign or NodeType.Orsign)
        {
            node.SignStates ??= new Dictionary<string, bool>();
            var who = signer ?? node.Approver ?? "";
            if (string.IsNullOrWhiteSpace(who))
            {
                throw new InvalidOperationException("会签/或签节点必须指定签署人");
            }
            node.SignStates[who] = true;

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
        EnsureNotFinished(flow);
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

        // 支持 amount 与常见比较运算符(>、>=、<、<=、==),无法识别的条件默认放行
        var match = Regex.Match(condition, @"amount\s*(>=|<=|==|>|<)\s*(\d+(?:\.\d+)?)");
        if (!match.Success)
        {
            return true;
        }

        var value = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return match.Groups[1].Value switch
        {
            ">" => flow.Amount > value,
            ">=" => flow.Amount >= value,
            "<" => flow.Amount < value,
            "<=" => flow.Amount <= value,
            "==" => flow.Amount == value,
            _ => true
        };
    }
}
