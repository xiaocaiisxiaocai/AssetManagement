using AssetManagement.Domain.Workflow;

namespace AssetManagement.Domain.Entities;

public class ApprovalFlow
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
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending";
    public int CurrentNodeIndex { get; set; }
    public List<FlowInstanceNode> Nodes { get; set; } = new();
    public DateTime ApplyTime { get; set; }
    public DateTime Deadline { get; set; }
}
