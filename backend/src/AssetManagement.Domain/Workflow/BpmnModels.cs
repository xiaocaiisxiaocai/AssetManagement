namespace AssetManagement.Domain.Workflow;

/// <summary>
/// BPMN 节点类型
/// </summary>
public enum BpmnNodeType
{
    StartEvent,        // 开始事件
    EndEvent,          // 结束事件
    UserTask,          // 用户任务（审批节点）
    ServiceTask,       // 服务任务（自动化）
    ExclusiveGateway,  // 排他网关（条件分支，走一条）
    ParallelGateway,   // 并行网关（所有分支同时走）
    InclusiveGateway   // 包容网关（满足条件的分支都走）
}

/// <summary>
/// BPMN 节点
/// </summary>
public class BpmnNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public BpmnNodeType Type { get; set; }

    /// <summary>
    /// 自定义属性（审批人配置等），从 extensionElements 解析
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// BPMN 连线
/// </summary>
public class BpmnFlow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceRef { get; set; } = "";
    public string TargetRef { get; set; } = "";

    /// <summary>
    /// 条件表达式（用于排他网关出边）
    /// </summary>
    public string? ConditionExpression { get; set; }
}

/// <summary>
/// BPMN 流程定义
/// </summary>
public class BpmnProcess
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<BpmnNode> Nodes { get; set; } = new();
    public List<BpmnFlow> Flows { get; set; } = new();

    /// <summary>
    /// 根据节点 ID 查找节点
    /// </summary>
    public BpmnNode? FindNode(string nodeId)
    {
        return Nodes.FirstOrDefault(n => n.Id == nodeId);
    }

    /// <summary>
    /// 获取节点的所有出边
    /// </summary>
    public List<BpmnFlow> GetOutgoingFlows(string nodeId)
    {
        return Flows.Where(f => f.SourceRef == nodeId).ToList();
    }

    /// <summary>
    /// 获取节点的所有入边
    /// </summary>
    public List<BpmnFlow> GetIncomingFlows(string nodeId)
    {
        return Flows.Where(f => f.TargetRef == nodeId).ToList();
    }
}

/// <summary>
/// BPMN Token 状态（流程实例中每个节点的执行状态）
/// </summary>
public enum BpmnTokenStatus
{
    Active,      // 活跃（等待审批）
    Completed,   // 已完成
    Skipped,     // 已跳过
    Waiting      // 等待（并行网关汇聚等待其他分支）
}

/// <summary>
/// BPMN Token（流程实例的执行令牌）
/// </summary>
public class BpmnToken
{
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public BpmnTokenStatus Status { get; set; }

    /// <summary>
    /// 审批人（UserTask 完成后记录）
    /// </summary>
    public string? Approver { get; set; }

    /// <summary>
    /// 审批意见
    /// </summary>
    public string? Opinion { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 会签成员状态（UserTask 的会签场景）
    /// </summary>
    public Dictionary<string, bool>? SignStates { get; set; }
}
