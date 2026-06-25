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

    /// <summary>当前活跃的节点 ID 列表（BPMN 支持并行）</summary>
    public List<string> CurrentNodeIds { get; set; } = new();

    /// <summary>BPMN Token 状态字典（节点ID -> Token状态）</summary>
    public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();

    public DateTime ApplyTime { get; set; }
    public DateTime Deadline { get; set; }
}
