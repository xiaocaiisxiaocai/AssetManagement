using System.Text.RegularExpressions;

namespace AssetManagement.Domain.Workflow;

/// <summary>
/// BPMN 流程实例状态（供引擎操作）
/// </summary>
public interface IBpmnFlowInstance
{
    Dictionary<string, BpmnToken> BpmnTokens { get; set; }
    List<string> CurrentNodeIds { get; set; }
    string Status { get; set; }
    string? ApplicantDept { get; }
}

/// <summary>
/// BPMN 执行引擎（基于 Token 驱动）
/// </summary>
public static class BpmnEngine
{
    /// <summary>
    /// 启动流程实例
    /// </summary>
    public static void Start(IBpmnFlowInstance flow, BpmnProcess process)
    {
        // 找到开始事件
        var startNode = process.Nodes.FirstOrDefault(n => n.Type == BpmnNodeType.StartEvent)
            ?? throw new InvalidOperationException("流程缺少开始事件");

        // 初始化 Token 状态
        flow.BpmnTokens = new Dictionary<string, BpmnToken>
        {
            [startNode.Id] = new BpmnToken
            {
                NodeId = startNode.Id,
                NodeName = startNode.Name,
                Status = BpmnTokenStatus.Completed,
                CompletedAt = DateTime.UtcNow
            }
        };

        flow.CurrentNodeIds = new List<string>();

        // 从开始事件推进
        AdvanceFrom(flow, process, startNode.Id);
    }

    /// <summary>
    /// 审批通过
    /// </summary>
    public static void Approve(IBpmnFlowInstance flow, BpmnProcess process, string nodeId, string approver, string? opinion = null)
    {
        if (!flow.BpmnTokens.TryGetValue(nodeId, out var token))
            throw new InvalidOperationException($"节点 {nodeId} 不存在活跃的 Token");

        if (token.Status != BpmnTokenStatus.Active)
            throw new InvalidOperationException($"节点 {nodeId} 当前不可审批");

        var node = process.FindNode(nodeId)
            ?? throw new InvalidOperationException($"节点 {nodeId} 不存在");

        if (node.Type != BpmnNodeType.UserTask)
            throw new InvalidOperationException($"节点 {nodeId} 不是用户任务");

        if (token.SignStates is { Count: > 0 })
        {
            if (!token.SignStates.ContainsKey(approver))
                throw new InvalidOperationException($"{approver} 不在节点 {nodeId} 的会签人列表中");

            token.SignStates[approver] = true;
            token.Approver = approver;
            token.Opinion = opinion;

            if (token.SignStates.Values.Any(signed => !signed))
                return;
        }

        // 标记 Token 完成
        token.Status = BpmnTokenStatus.Completed;
        token.Approver = approver;
        token.Opinion = opinion;
        token.CompletedAt = DateTime.UtcNow;

        // 从当前节点推进
        AdvanceFrom(flow, process, nodeId);
    }

    /// <summary>
    /// 驳回流程
    /// </summary>
    public static void Reject(IBpmnFlowInstance flow, string nodeId, string rejector, string reason)
    {
        if (!flow.BpmnTokens.TryGetValue(nodeId, out var token))
            throw new InvalidOperationException($"节点 {nodeId} 不存在活跃的 Token");

        if (token.Status != BpmnTokenStatus.Active)
            throw new InvalidOperationException($"节点 {nodeId} 当前不可审批");

        // 标记流程为已驳回
        flow.Status = "rejected";
        flow.CurrentNodeIds.Clear();

        token.Status = BpmnTokenStatus.Completed;
        token.Approver = rejector;
        token.Opinion = $"[驳回] {reason}";
        token.CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 从指定节点推进流程
    /// </summary>
    private static void AdvanceFrom(IBpmnFlowInstance flow, BpmnProcess process, string fromNodeId)
    {
        // 移除当前节点 ID
        flow.CurrentNodeIds.Remove(fromNodeId);

        var fromNode = process.FindNode(fromNodeId)!;
        var outgoingFlows = process.GetOutgoingFlows(fromNodeId);

        if (outgoingFlows.Count == 0)
        {
            // 到达结束节点
            CheckCompletion(flow, process);
            return;
        }

        switch (fromNode.Type)
        {
            case BpmnNodeType.StartEvent:
            case BpmnNodeType.UserTask:
            case BpmnNodeType.ServiceTask:
                // 普通节点：单一出边
                if (outgoingFlows.Count != 1)
                    throw new InvalidOperationException($"节点 {fromNodeId} 应该只有一个出边");

                MoveToken(flow, process, outgoingFlows[0].TargetRef);
                break;

            case BpmnNodeType.ExclusiveGateway:
                // 排他网关：选择第一个满足条件的分支
                HandleExclusiveGateway(flow, process, outgoingFlows);
                break;

            case BpmnNodeType.ParallelGateway:
                // 并行网关：判断是分叉还是汇聚
                HandleParallelGateway(flow, process, fromNodeId, outgoingFlows);
                break;

            case BpmnNodeType.InclusiveGateway:
                // 包容网关：所有满足条件的分支都激活
                HandleInclusiveGateway(flow, process, outgoingFlows);
                break;
        }
    }

    /// <summary>
    /// 处理排他网关（选择一条分支）
    /// </summary>
    private static void HandleExclusiveGateway(IBpmnFlowInstance flow, BpmnProcess process, List<BpmnFlow> outgoingFlows)
    {
        // 按顺序评估条件，走第一个满足的分支
        foreach (var outFlow in outgoingFlows)
        {
            if (string.IsNullOrEmpty(outFlow.ConditionExpression) || EvaluateCondition(flow, outFlow.ConditionExpression))
            {
                MoveToken(flow, process, outFlow.TargetRef);
                return;
            }
        }

        throw new InvalidOperationException("排他网关没有满足条件的分支");
    }

    /// <summary>
    /// 处理并行网关（分叉所有分支 或 汇聚等待）
    /// </summary>
    private static void HandleParallelGateway(IBpmnFlowInstance flow, BpmnProcess process, string gatewayId, List<BpmnFlow> outgoingFlows)
    {
        var incomingFlows = process.GetIncomingFlows(gatewayId);

        if (incomingFlows.Count > 1)
        {
            // 汇聚点：检查所有入边是否都已完成
            var allIncomingCompleted = incomingFlows.All(inFlow =>
            {
                var sourceToken = flow.BpmnTokens.GetValueOrDefault(inFlow.SourceRef);
                return sourceToken?.Status == BpmnTokenStatus.Completed;
            });

            if (!allIncomingCompleted)
            {
                // 还有分支未完成，标记为等待
                flow.BpmnTokens[gatewayId] = new BpmnToken
                {
                    NodeId = gatewayId,
                    NodeName = "并行汇聚",
                    Status = BpmnTokenStatus.Waiting
                };
                return;
            }

            // 所有分支已完成，继续推进
            flow.BpmnTokens[gatewayId] = new BpmnToken
            {
                NodeId = gatewayId,
                NodeName = "并行汇聚",
                Status = BpmnTokenStatus.Completed,
                CompletedAt = DateTime.UtcNow
            };
        }

        // 分叉点或汇聚完成：激活所有出边
        foreach (var outFlow in outgoingFlows)
        {
            MoveToken(flow, process, outFlow.TargetRef);
        }
    }

    /// <summary>
    /// 处理包容网关（激活所有满足条件的分支）
    /// </summary>
    private static void HandleInclusiveGateway(IBpmnFlowInstance flow, BpmnProcess process, List<BpmnFlow> outgoingFlows)
    {
        var activatedAny = false;

        foreach (var outFlow in outgoingFlows)
        {
            if (string.IsNullOrEmpty(outFlow.ConditionExpression) || EvaluateCondition(flow, outFlow.ConditionExpression))
            {
                MoveToken(flow, process, outFlow.TargetRef);
                activatedAny = true;
            }
        }

        if (!activatedAny)
            throw new InvalidOperationException("包容网关没有满足条件的分支");
    }

    /// <summary>
    /// 移动 Token 到目标节点
    /// </summary>
    private static void MoveToken(IBpmnFlowInstance flow, BpmnProcess process, string toNodeId)
    {
        var toNode = process.FindNode(toNodeId)
            ?? throw new InvalidOperationException($"节点 {toNodeId} 不存在");

        switch (toNode.Type)
        {
            case BpmnNodeType.UserTask:
                // 用户任务：激活并等待审批
                flow.BpmnTokens[toNodeId] = new BpmnToken
                {
                    NodeId = toNodeId,
                    NodeName = toNode.Name,
                    Status = BpmnTokenStatus.Active,
                    SignStates = BuildSignStates(toNode)
                };
                flow.CurrentNodeIds.Add(toNodeId);
                break;

            case BpmnNodeType.ServiceTask:
                // 服务任务：自动完成
                flow.BpmnTokens[toNodeId] = new BpmnToken
                {
                    NodeId = toNodeId,
                    NodeName = toNode.Name,
                    Status = BpmnTokenStatus.Completed,
                    CompletedAt = DateTime.UtcNow
                };
                AdvanceFrom(flow, process, toNodeId);
                break;

            case BpmnNodeType.EndEvent:
                // 结束事件：标记完成
                flow.BpmnTokens[toNodeId] = new BpmnToken
                {
                    NodeId = toNodeId,
                    NodeName = toNode.Name,
                    Status = BpmnTokenStatus.Completed,
                    CompletedAt = DateTime.UtcNow
                };
                CheckCompletion(flow, process);
                break;

            case BpmnNodeType.ExclusiveGateway:
            case BpmnNodeType.ParallelGateway:
            case BpmnNodeType.InclusiveGateway:
                // 网关：自动完成并继续推进
                flow.BpmnTokens[toNodeId] = new BpmnToken
                {
                    NodeId = toNodeId,
                    NodeName = toNode.Name,
                    Status = BpmnTokenStatus.Completed,
                    CompletedAt = DateTime.UtcNow
                };
                AdvanceFrom(flow, process, toNodeId);
                break;
        }
    }

    /// <summary>
    /// 检查流程是否完成
    /// </summary>
    private static void CheckCompletion(IBpmnFlowInstance flow, BpmnProcess process)
    {
        // 如果所有 Token 都完成，且没有活跃节点，则流程完成
        if (flow.CurrentNodeIds.Count == 0 &&
            flow.BpmnTokens.Values.All(t => t.Status == BpmnTokenStatus.Completed || t.Status == BpmnTokenStatus.Skipped))
        {
            flow.Status = "approved";
        }
    }

    /// <summary>
    /// 条件表达式求值
    /// </summary>
    private static bool EvaluateCondition(IBpmnFlowInstance flow, string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        condition = condition.Trim();

        var deptMatch = Regex.Match(condition, @"^\$\{applicantDept\}\s*==\s*['""](.+?)['""]$");
        if (deptMatch.Success)
        {
            var right = deptMatch.Groups[1].Value;
            return string.Equals(flow.ApplicantDept, right, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"无法识别的条件表达式: {condition}");
    }

    private static Dictionary<string, bool>? BuildSignStates(BpmnNode node)
    {
        if (!node.Properties.TryGetValue("approvalMode", out var mode) || mode != "all")
            return null;

        var signers = new List<string>();
        if (node.Properties.TryGetValue("assignee", out var assignee) && !string.IsNullOrWhiteSpace(assignee))
        {
            signers.AddRange(assignee
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (node.Properties.TryGetValue("candidateUsers", out var candidateUsers))
        {
            signers.AddRange(candidateUsers
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return signers.Distinct().ToDictionary(signer => signer, _ => false);
    }
}
