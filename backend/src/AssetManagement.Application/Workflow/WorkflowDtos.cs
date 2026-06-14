using AssetManagement.Domain.Workflow;

namespace AssetManagement.Application.Workflow;

public record WorkflowDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string BizType { get; init; } = "";
    public List<WorkflowNode> Nodes { get; init; } = new();
}

public record SaveWorkflowRequest
{
    public string Name { get; init; } = "";
    public string BizType { get; init; } = "";
    public List<WorkflowNode> Nodes { get; init; } = new();
}

public record ApprovalFlowDto
{
    public int Id { get; init; }
    public string FlowNo { get; init; } = "";
    public string BizType { get; init; } = "";
    public int AssetId { get; init; }
    public string AssetNo { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string Applicant { get; init; } = "";
    public string? ApplicantDept { get; init; }
    public string? Transferee { get; init; }
    public string? TransfereeDept { get; init; }
    public string? Reason { get; init; }
    public string? ReturnDate { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = "";
    public int CurrentNodeIndex { get; init; }
    public List<FlowInstanceNode> Nodes { get; init; } = new();
    public DateTime ApplyTime { get; init; }
    public DateTime Deadline { get; init; }
}

public record StartApprovalRequest
{
    public string BizType { get; init; } = "";
    public int AssetId { get; init; }
    public int? TransfereeId { get; init; }
    public string? Reason { get; init; }
    public string? ReturnDate { get; init; }
}

public record ApprovalActionRequest
{
    public string? Signer { get; init; }
    public string Opinion { get; init; } = "";
}

public record RejectRequest
{
    public string Reason { get; init; } = "";
}

public record AddSignRequest
{
    public string Who { get; init; } = "";
}

public record TransferSignRequest
{
    public string Who { get; init; } = "";
}
