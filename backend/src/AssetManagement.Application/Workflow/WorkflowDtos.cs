using AssetManagement.Domain.Workflow;

namespace AssetManagement.Application.Workflow;

public record WorkflowDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string BizType { get; init; } = "";
    public string? BpmnXml { get; init; }
}

public record SaveWorkflowRequest
{
    public string Name { get; init; } = "";
    public string BizType { get; init; } = "";
    public string? BpmnXml { get; init; }
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
    public string Status { get; init; } = "";
    public List<string> CurrentNodeIds { get; init; } = new();
    public Dictionary<string, BpmnToken> BpmnTokens { get; init; } = new();
    public DateTime ApplyTime { get; init; }
    public DateTime Deadline { get; init; }
    public DateTime? ConfirmedAt { get; init; }
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
    public string? NodeId { get; init; }  // BPMN 模式下需要指定节点 ID
    public string Opinion { get; init; } = "";
}

public record RejectRequest
{
    public string? NodeId { get; init; }  // BPMN 模式下需要指定节点 ID
    public string Reason { get; init; } = "";
}

public record AddSignRequest
{
    public string? NodeId { get; init; }
    public string Who { get; init; } = "";
}

public record TransferSignRequest
{
    public string Who { get; init; } = "";
}
