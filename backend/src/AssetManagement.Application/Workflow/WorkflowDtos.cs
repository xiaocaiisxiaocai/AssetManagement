using AssetManagement.Domain.Workflow;
using System.ComponentModel.DataAnnotations;

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
    [Required, MaxLength(100)]
    public string Name { get; init; } = "";
    [Required, MaxLength(50)]
    public string BizType { get; init; } = "";
    public string? BpmnXml { get; init; }
}

public record UpdateWorkflowMetadataRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = "";
    [Required, MaxLength(50)]
    public string BizType { get; init; } = "";
}

public record DesignWorkflowRequest
{
    public string? BpmnXml { get; init; }
}

public record ApprovalFlowPageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public string? BizType { get; init; }
    public string? Status { get; init; }
    public int? FlowId { get; init; }
    public string? ReturnDate { get; init; }
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
    public string? OriginalReturnDate { get; init; }
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
    /// <summary>pending / completed / rejected / skipped</summary>
    public string Status { get; init; } = "pending";
}

public record StartApprovalRequest
{
    [Required, StringLength(50)]
    public string BizType { get; init; } = "";
    public int AssetId { get; init; }
    public int? TransfereeId { get; init; }
    [StringLength(500)]
    public string? Reason { get; init; }
    [StringLength(50)]
    public string? ReturnDate { get; init; }
}

public record ApprovalActionRequest
{
    [StringLength(100)]
    public string? NodeId { get; init; }  // BPMN 模式下需要指定节点 ID
    [StringLength(380)]
    public string Opinion { get; init; } = "";
}

public record RejectRequest
{
    [StringLength(100)]
    public string? NodeId { get; init; }  // BPMN 模式下需要指定节点 ID
    [Required, StringLength(500)]
    public string Reason { get; init; } = "";
}

public record AddSignRequest
{
    [StringLength(100)]
    public string? NodeId { get; init; }
    [Required, StringLength(100)]
    public string Who { get; init; } = "";
}

public record CancelAddSignRequest
{
    [StringLength(100)]
    public string? NodeId { get; init; }
    [Required, StringLength(100)]
    public string Who { get; init; } = "";
}

public record TransferSignRequest
{
    [Required, StringLength(100)]
    public string Who { get; init; } = "";
}
