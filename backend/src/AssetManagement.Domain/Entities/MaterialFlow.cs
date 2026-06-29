using AssetManagement.Domain.Workflow;

namespace AssetManagement.Domain.Entities;

public class MaterialFlow : IBpmnFlowInstance
{
    public int Id { get; set; }
    public string FlowNo { get; set; } = "";
    public string BizType { get; set; } = "material_transfer";
    public int WorkflowId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialNo { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public int ApplicantId { get; set; }
    public string Applicant { get; set; } = "";
    public string? ApplicantDept { get; set; }
    public int? TransfereeId { get; set; }
    public string? Transferee { get; set; }
    public string? TransfereeDept { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "pending";

    /// <summary>标记为直接转移（跳过审批），区分审批转移</summary>
    public bool DirectTransfer { get; set; }

    /// <summary>当前活跃的节点 ID 列表（BPMN 支持并行）</summary>
    public List<string> CurrentNodeIds { get; set; } = new();

    /// <summary>BPMN Token 状态字典（节点ID -> Token状态）</summary>
    public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();

    public DateTime ApplyTime { get; set; }
    public DateTime Deadline { get; set; }

    /// <summary>乐观并发令牌，防止两个审批人同时操作同一流转单</summary>
    public uint RowVersion { get; set; }

    /// <summary>条件表达式上下文变量，供 BpmnEngine 条件求值使用</summary>
    public Dictionary<string, string>? Context { get; set; }
}
