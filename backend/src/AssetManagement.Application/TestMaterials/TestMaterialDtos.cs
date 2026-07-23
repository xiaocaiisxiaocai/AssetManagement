using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Application.Workflow;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.TestMaterials;

// ===== 测试项目 =====
public class TestProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? ProjectTypeCode { get; set; }
    public string? ProjectTypeLabel { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? ProgressCode { get; set; }
    public string? ProgressLabel { get; set; }
    public int? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string? TestStatus { get; set; }
    public int FollowUpIntervalDays { get; set; }
    public DateTime? NextFollowUpDueDate { get; set; }
    public string FollowUpStatus { get; set; } = "";
    public string? LatestFollowUpContent { get; set; }
    public DateTime? LatestFollowUpAt { get; set; }
    public bool CanWriteFollowUp { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int MaterialCount { get; set; }
}

public class SaveTestProjectRequest
{
    [Required(ErrorMessage = "项目名称不能为空"), StringLength(100)]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "项目编号不能为空"), StringLength(50)]
    public string? Code { get; set; }
    [Required(ErrorMessage = "项目类型不能为空"), StringLength(50)]
    public string? ProjectTypeCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    [Required(ErrorMessage = "项目进度不能为空"), StringLength(50)]
    public string? ProgressCode { get; set; }
    public int? OwnerId { get; set; }
    [StringLength(1000)]
    public string? TestStatus { get; set; }
    public int FollowUpIntervalDays { get; set; } = 14;
}

public class TestProjectImportRowDto
{
    public int Row { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProjectTypeCode { get; set; } = "";
    public string ProjectTypeLabel { get; set; } = "";
    public string OwnerEmployeeNo { get; set; } = "";
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public string ProgressCode { get; set; } = "";
    public string ProgressLabel { get; set; } = "";
    public DateTime? ClosedDate { get; set; }
    public int FollowUpIntervalDays { get; set; } = 14;
    public string? TestStatus { get; set; }
    public bool IsValid { get; set; }
    public string Error { get; set; } = "";
}

public class TestProjectImportResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<TestProjectImportRowDto> Rows { get; set; } = new();
}

public class UpdateTestProjectProgressRequest
{
    [Required(ErrorMessage = "项目进度不能为空"), StringLength(50)]
    public string? ProgressCode { get; set; }
    public DateTime? ClosedDate { get; set; }
    [StringLength(1000)]
    public string? TestStatus { get; set; }
}

public class TestProjectOptionDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SaveTestProjectOptionRequest
{
    [Required, StringLength(50)]
    public string Kind { get; set; } = "";
    [Required, StringLength(50)]
    public string Code { get; set; } = "";
    [Required, StringLength(100)]
    public string Label { get; set; } = "";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TestProjectFollowupDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public DateTime DueDate { get; set; }
    public string Content { get; set; } = "";
    public int FilledById { get; set; }
    public string? FilledByName { get; set; }
    public DateTime FilledAt { get; set; }
}

public class SaveTestProjectFollowupRequest
{
    public DateTime? DueDate { get; set; }
    [Required, StringLength(2000)]
    public string Content { get; set; } = "";
}

// ===== 测试料件 =====
public class TestMaterialDto
{
    public int Id { get; set; }
    public string MaterialNo { get; set; } = "";
    public string Name { get; set; } = "";
    public int ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? VendorName { get; set; }
    public string? Model { get; set; }
    public string? Brand { get; set; }
    public int Quantity { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? LocationName { get; set; }
    public int? CustodianId { get; set; }
    public string? CustodianName { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public MaterialStatus Status { get; set; }
    public List<string> Images { get; set; } = new();
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    /// <summary>是否存在进行中的流转单(派生锁定标志)</summary>
    public bool HasPendingFlow { get; set; }
}

public class TestMaterialQuery
{
    public string? MaterialNo { get; set; }
    public string? Name { get; set; }
    public int? ProjectId { get; set; }
    public int? DepartmentId { get; set; }
    public MaterialStatus? Status { get; set; }
    /// <summary>active(默认未删除) / all(全部) / deleted(仅已删除)</summary>
    public string? DeleteStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SaveTestMaterialRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    public int ProjectId { get; set; }
    [StringLength(100)]
    public string? VendorName { get; set; }
    [StringLength(100)]
    public string? Model { get; set; }
    [StringLength(100)]
    public string? Brand { get; set; }
    public int Quantity { get; set; } = 1;
    public int? DepartmentId { get; set; }
    [StringLength(100)]
    public string? LocationName { get; set; }
    public int? CustodianId { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public List<string>? Images { get; set; }
    [StringLength(500)]
    public string? Remark { get; set; }
}

public class MaterialFlowDto
{
    public int Id { get; set; }
    public string FlowNo { get; set; } = "";
    public string BizType { get; set; } = "";
    public int MaterialId { get; set; }
    public string MaterialNo { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Applicant { get; set; } = "";
    public string? ApplicantDept { get; set; }
    public string? Transferee { get; set; }
    public string? TransfereeDept { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "";
    public List<string> CurrentNodeIds { get; set; } = new();
    /// <summary>当前调用人可处理的活跃节点；CurrentNodeIds 仍表示流程的全部活跃节点。</summary>
    public List<string> ActionableNodeIds { get; set; } = new();
    public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();
    public DateTime ApplyTime { get; set; }
    public DateTime Deadline { get; set; }
    /// <summary>当无需审批直接转移时为 true</summary>
    public bool DirectTransfer { get; set; }
    /// <summary>当前调用人是否可撤回该流转申请。</summary>
    public bool CanWithdraw { get; set; }
    /// <summary>当前调用人最近一次审批动作：approve / reject。</summary>
    public string? MyApprovalAction { get; set; }
    public string? MyApprovalNodeId { get; set; }
    public DateTime? MyApprovalTime { get; set; }
    public List<WorkflowProgressStepDto> ProgressSteps { get; set; } = new();
    public List<WorkflowProgressStepDto> CurrentSteps { get; set; } = new();
    public List<WorkflowProgressStepDto> NextSteps { get; set; } = new();
}

public class TestProjectPageQuery
{
    public string? DeleteStatus { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
    public int? OwnerId { get; init; }
    public string? ProgressCode { get; init; }
    public string? ProjectTypeCode { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class MaterialFlowPageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public int? ProjectId { get; set; }
    public int? FlowId { get; set; }
}

public class MaterialFlowRecordDto
{
    public string Key { get; set; } = "";
    public int Id { get; set; }
    public string Action { get; set; } = "";
    public string? Operator { get; set; }
    public string? Comment { get; set; }
    public DateTime OperatedAt { get; set; }
}

public class TestMaterialDetailDto
{
    public TestMaterialDto Material { get; set; } = new();
    public List<MaterialFlowDto> Flows { get; set; } = new();
    public List<MaterialFlowRecordDto> Records { get; set; } = new();
}

public class InitiateTransferRequest
{
    public int MaterialId { get; set; }
    public int TransfereeId { get; set; }
    [StringLength(500)]
    public string? Reason { get; set; }
}

public class MaterialApprovalRequest
{
    [StringLength(100)]
    public string? NodeId { get; set; }
    [StringLength(380)]
    public string? Opinion { get; set; }
}

public class MaterialRejectRequest
{
    [StringLength(100)]
    public string? NodeId { get; set; }
    [Required, StringLength(500)]
    public string Reason { get; set; } = "";
}
