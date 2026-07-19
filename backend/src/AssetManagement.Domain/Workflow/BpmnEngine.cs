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
    Dictionary<string, string>? Context { get; }
}

/// <summary>
/// BPMN 执行引擎（基于 Token 驱动）
/// </summary>
public static class BpmnEngine
{
    private const int MaxAutomaticTransitions = 1024;

    private sealed class AutomaticTraversal
    {
        public int Steps { get; set; }
        public HashSet<string> Path { get; } = new(StringComparer.Ordinal);

        public AutomaticTraversal Branch()
        {
            var branch = new AutomaticTraversal { Steps = Steps };
            branch.Path.UnionWith(Path);
            return branch;
        }

        public void Enter(string nodeId)
        {
            if (++Steps > MaxAutomaticTransitions)
                throw new InvalidOperationException($"流程自动推进超过 {MaxAutomaticTransitions} 步，请检查流程是否存在循环");
            if (!Path.Add(nodeId))
                throw new InvalidOperationException($"检测到自动节点循环: {nodeId}");
        }
    }
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
        AdvanceFrom(flow, process, startNode.Id, new AutomaticTraversal());
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
        // 人工任务是合法循环的边界：每次人工审批后重新开始一次自动推进检查。
        AdvanceFrom(flow, process, nodeId, new AutomaticTraversal());
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

        foreach (var remaining in flow.BpmnTokens.Values.Where(item =>
                     !ReferenceEquals(item, token) &&
                     item.Status is BpmnTokenStatus.Active or BpmnTokenStatus.Waiting))
        {
            remaining.Status = BpmnTokenStatus.Skipped;
            remaining.Approver = rejector;
            remaining.Opinion = "[终止] 同一流程的其他分支已驳回";
            remaining.CompletedAt = token.CompletedAt;
        }
    }

    /// <summary>
    /// 申请人撤回进行中的流程
    /// </summary>
    public static void Withdraw(IBpmnFlowInstance flow, string applicant)
    {
        flow.Status = FlowStatus.Withdrawn;
        flow.CurrentNodeIds.Clear();

        var completedAt = DateTime.UtcNow;
        foreach (var token in flow.BpmnTokens.Values.Where(token =>
                     token.Status is BpmnTokenStatus.Active or BpmnTokenStatus.Waiting))
        {
            token.Status = BpmnTokenStatus.Skipped;
            token.Approver = applicant;
            token.Opinion = "[撤回] 申请人主动撤回";
            token.CompletedAt = completedAt;
        }
    }

    /// <summary>
    /// 从指定节点推进流程
    /// </summary>
    private static void AdvanceFrom(
        IBpmnFlowInstance flow,
        BpmnProcess process,
        string fromNodeId,
        AutomaticTraversal traversal)
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

                MoveToken(flow, process, outgoingFlows[0].TargetRef, traversal);
                break;

            case BpmnNodeType.ExclusiveGateway:
                // 排他网关既可以分叉，也可以作为标准的简单汇聚点。
                if (process.GetIncomingFlows(fromNodeId).Count > 1 && outgoingFlows.Count == 1)
                    MoveToken(flow, process, outgoingFlows[0].TargetRef, traversal);
                else
                    HandleExclusiveGateway(flow, process, outgoingFlows, traversal);
                break;

            case BpmnNodeType.ParallelGateway:
                // 并行网关：判断是分叉还是汇聚
                HandleParallelGateway(flow, process, fromNodeId, outgoingFlows, traversal);
                break;

            case BpmnNodeType.InclusiveGateway:
                // 包容网关汇聚只等待本次实际激活、且仍能到达该汇聚点的分支。
                if (process.GetIncomingFlows(fromNodeId).Count > 1 && outgoingFlows.Count == 1)
                    HandleInclusiveJoin(flow, process, fromNodeId, outgoingFlows[0], traversal);
                else
                    HandleInclusiveGateway(flow, process, outgoingFlows, traversal);
                break;
        }
    }

    /// <summary>
    /// 处理排他网关（选择一条分支）
    /// </summary>
    private static void HandleExclusiveGateway(IBpmnFlowInstance flow, BpmnProcess process, List<BpmnFlow> outgoingFlows, AutomaticTraversal traversal)
    {
        // 优先走有条件且满足的分支，无条件分支作为兜底默认
        BpmnFlow? defaultFlow = null;
        foreach (var outFlow in outgoingFlows)
        {
            if (string.IsNullOrEmpty(outFlow.ConditionExpression))
            {
                defaultFlow ??= outFlow;
                continue;
            }
            if (EvaluateCondition(flow, outFlow.ConditionExpression))
            {
                MoveToken(flow, process, outFlow.TargetRef, traversal);
                return;
            }
        }

        if (defaultFlow != null)
        {
            MoveToken(flow, process, defaultFlow.TargetRef, traversal);
            return;
        }

        throw new InvalidOperationException("排他网关没有满足条件的分支");
    }

    /// <summary>
    /// 处理并行网关（分叉所有分支 或 汇聚等待）
    /// </summary>
    private static void HandleParallelGateway(IBpmnFlowInstance flow, BpmnProcess process, string gatewayId, List<BpmnFlow> outgoingFlows, AutomaticTraversal traversal)
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
            MoveToken(flow, process, outFlow.TargetRef, traversal.Branch());
        }
    }

    /// <summary>
    /// 处理包容网关（激活所有满足条件的分支）
    /// </summary>
    private static void HandleInclusiveGateway(IBpmnFlowInstance flow, BpmnProcess process, List<BpmnFlow> outgoingFlows, AutomaticTraversal traversal)
    {
        var activatedAny = false;

        foreach (var outFlow in outgoingFlows)
        {
            if (string.IsNullOrEmpty(outFlow.ConditionExpression) || EvaluateCondition(flow, outFlow.ConditionExpression))
            {
                MoveToken(flow, process, outFlow.TargetRef, traversal.Branch());
                activatedAny = true;
            }
        }

        if (!activatedAny)
            throw new InvalidOperationException("包容网关没有满足条件的分支");
    }

    private static void HandleInclusiveJoin(
        IBpmnFlowInstance flow,
        BpmnProcess process,
        string gatewayId,
        BpmnFlow outgoingFlow,
        AutomaticTraversal traversal)
    {
        // 第一个分支到达汇聚点时，其他已激活分支通常仍停在 UserTask。
        // 只等待能够沿后续路径到达本汇聚点的活跃节点，未被条件选中的分支没有
        // 活跃 Token，因此不会错误地阻塞包容汇聚。
        var hasOtherActiveBranch = flow.CurrentNodeIds.Any(nodeId =>
            !string.Equals(nodeId, gatewayId, StringComparison.Ordinal) &&
            CanReach(process, nodeId, gatewayId));
        if (hasOtherActiveBranch)
        {
            flow.BpmnTokens[gatewayId] = new BpmnToken
            {
                NodeId = gatewayId,
                NodeName = process.FindNode(gatewayId)?.Name ?? "包容汇聚",
                Status = BpmnTokenStatus.Waiting,
                StartedAt = DateTime.UtcNow
            };
            return;
        }

        flow.BpmnTokens[gatewayId] = new BpmnToken
        {
            NodeId = gatewayId,
            NodeName = process.FindNode(gatewayId)?.Name ?? "包容汇聚",
            Status = BpmnTokenStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        MoveToken(flow, process, outgoingFlow.TargetRef, traversal);
    }

    private static bool CanReach(BpmnProcess process, string sourceId, string targetId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(sourceId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            foreach (var edge in process.GetOutgoingFlows(current))
            {
                if (edge.TargetRef == targetId) return true;
                pending.Push(edge.TargetRef);
            }
        }
        return false;
    }

    /// <summary>
    /// 移动 Token 到目标节点
    /// </summary>
    private static void MoveToken(IBpmnFlowInstance flow, BpmnProcess process, string toNodeId, AutomaticTraversal traversal)
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
                    StartedAt = DateTime.UtcNow,
                    SignStates = BuildSignStates(toNode)
                };
                if (!flow.CurrentNodeIds.Contains(toNodeId))
                    flow.CurrentNodeIds.Add(toNodeId);
                break;

            case BpmnNodeType.ServiceTask:
                traversal.Enter(toNodeId);
                flow.BpmnTokens[toNodeId] = new BpmnToken
                {
                    NodeId = toNodeId,
                    NodeName = toNode.Name,
                    Status = BpmnTokenStatus.Completed,
                    CompletedAt = DateTime.UtcNow
                };
                AdvanceFrom(flow, process, toNodeId, traversal);
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
                traversal.Enter(toNodeId);
                flow.BpmnTokens[toNodeId] = new BpmnToken
                {
                    NodeId = toNodeId,
                    NodeName = toNode.Name,
                    Status = BpmnTokenStatus.Completed,
                    CompletedAt = DateTime.UtcNow
                };
                AdvanceFrom(flow, process, toNodeId, traversal);
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

        var stringMatch = Regex.Match(condition, @"^\$\{(\w+)\}\s*(==|!=)\s*['""](.+?)['""]$");
        if (stringMatch.Success)
        {
            var varName = stringMatch.Groups[1].Value;
            var op = stringMatch.Groups[2].Value;
            var right = stringMatch.Groups[3].Value;
            if (flow.Context is null || !flow.Context.TryGetValue(varName, out var left))
            {
                throw new InvalidOperationException($"无法识别的条件表达式: {condition}");
            }

            return op == "=="
                ? string.Equals(left, right, StringComparison.Ordinal)
                : !string.Equals(left, right, StringComparison.Ordinal);
        }

        var numMatch = Regex.Match(condition, @"^\$\{(\w+)\}\s*(>=|<=|!=|==|>|<)\s*(\d+(?:\.\d+)?)$");
        if (numMatch.Success)
        {
            var varName = numMatch.Groups[1].Value;
            var op = numMatch.Groups[2].Value;
            var right = decimal.Parse(numMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            var rawVal = flow.Context?.GetValueOrDefault(varName);
            if (!decimal.TryParse(rawVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var left))
                return false;
            return op switch
            {
                ">" => left > right, "<" => left < right,
                ">=" => left >= right, "<=" => left <= right,
                "==" => left == right, "!=" => left != right,
                _ => throw new InvalidOperationException($"不支持的运算符: {op}")
            };
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
