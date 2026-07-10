using AssetManagement.Domain.Workflow;

namespace AssetManagement.Domain.Entities;

public class ApprovalFlow : IBpmnFlowInstance
{
    public int Id { get; set; }
    public string FlowNo { get; set; } = "";
    public string BizType { get; set; } = "";
    public int WorkflowId { get; set; }
    public int AssetId { get; set; }
    public string AssetNo { get; set; } = "";
    public string AssetName { get; set; } = "";
    public int ApplicantId { get; set; }
    public string Applicant { get; set; } = "";
    public string? ApplicantDept { get; set; }
    public int? TransfereeId { get; set; }
    public string? Transferee { get; set; }
    public string? TransfereeDept { get; set; }
    public string? Reason { get; set; }
    public string? ReturnDate { get; set; }
    public string Status { get; set; } = "pending";

    /// <summary>进行中流程的数据库唯一锁；结束后置空，允许保留任意数量历史记录。</summary>
    public string? ActiveScopeKey { get; set; }

    /// <summary>
    /// 当前活跃的节点 ID 列表（BPMN 模式支持多个并行节点）
    /// </summary>
    public List<string> CurrentNodeIds { get; set; } = new();

    /// <summary>
    /// BPMN Token 状态字典（节点ID -> Token状态）
    /// </summary>
    public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();

    public DateTime ApplyTime { get; set; }
    public DateTime Deadline { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>乐观并发令牌，防止两个审批人同时操作同一流程单</summary>
    public uint RowVersion { get; set; }

    /// <summary>条件表达式上下文变量（如 amount、quantity），供 BpmnEngine 条件求值使用</summary>
    public Dictionary<string, string>? Context { get; set; }
}
