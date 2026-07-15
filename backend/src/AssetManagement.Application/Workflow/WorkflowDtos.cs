using AssetManagement.Domain.Workflow;

namespace AssetManagement.Application.Workflow;

public record WorkflowDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string BizType { get; init; } = "";
    public string BizTypeLabel { get; init; } = "";
    public string? BpmnXml { get; init; }
    public bool IsActive { get; init; }
    /// <summary>empty=未配置 / configured=已配置且校验通过 / invalid=配置异常</summary>
    public string BpmnStatus { get; init; } = "empty";
    public List<string> BpmnValidationErrors { get; init; } = new();
}

public record SaveWorkflowRequest
{
    public string Name { get; init; } = "";
    public string BizType { get; init; } = "";
    public string? BpmnXml { get; init; }
}

public record SetWorkflowStatusRequest
{
    public bool IsActive { get; init; }
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
    /// <summary>当前调用人可处理的活跃节点；CurrentNodeIds 仍表示流程的全部活跃节点。</summary>
    public List<string> ActionableNodeIds { get; init; } = new();
    public Dictionary<string, BpmnToken> BpmnTokens { get; init; } = new();
    public DateTime ApplyTime { get; init; }
    public DateTime Deadline { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public List<WorkflowProgressStepDto> ProgressSteps { get; init; } = new();
    public List<WorkflowProgressStepDto> CurrentSteps { get; init; } = new();
    public List<WorkflowProgressStepDto> NextSteps { get; init; } = new();
}

public record WorkflowProgressStepDto
{
    public string NodeId { get; init; } = "";
    public string NodeName { get; init; } = "";
    /// <summary>completed / current / next</summary>
    public string State { get; init; } = "";
    public bool IsPossible { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? CompletedBy { get; init; }
    public string? Opinion { get; init; }
    public List<WorkflowAssigneeDto> Assignees { get; init; } = new();
}

public record WorkflowAssigneeDto
{
    public int UserId { get; init; }
    public string EmployeeNo { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>pending / completed</summary>
    public string Status { get; init; } = "pending";
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

public record CancelAddSignRequest
{
    public string? NodeId { get; init; }
    public string Who { get; init; } = "";
}

public record TransferSignRequest
{
    public string Who { get; init; } = "";
}
