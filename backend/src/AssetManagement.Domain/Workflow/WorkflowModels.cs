namespace AssetManagement.Domain.Workflow;

public enum NodeType
{
    Start,
    Approval,
    Countersign,
    Orsign,
    Condition,
    Cc,
    End
}

public enum ApproverType
{
    User,
    Role,
    Supervisor,
    DeptManager,
    ApplicantPick
}

public enum NodeStatus
{
    Pending,
    Current,
    Done,
    Skipped,
    Rejected
}

public class WorkflowNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public NodeType Type { get; set; }
    public ApproverType ApproverType { get; set; }
    public string? Approver { get; set; }
    public List<string>? Signers { get; set; }
    public string? Condition { get; set; }
    // 画布坐标（仅前端 LogicFlow 设计器布局用，执行引擎忽略；旧数据为 null 时前端自动布局）
    public double? X { get; set; }
    public double? Y { get; set; }
}

public class FlowInstanceNode
{
    public string Name { get; set; } = "";
    public NodeType Type { get; set; }
    public NodeStatus Status { get; set; } = NodeStatus.Pending;
    public string? Approver { get; set; }
    public string? Opinion { get; set; }
    public string? Time { get; set; }
    public List<string>? Signers { get; set; }
    public Dictionary<string, bool>? SignStates { get; set; }
    public List<string>? AddedSigners { get; set; }
    public string? Condition { get; set; }
}
