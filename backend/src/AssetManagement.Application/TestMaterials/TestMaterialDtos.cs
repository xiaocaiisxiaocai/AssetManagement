using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;

namespace AssetManagement.Application.TestMaterials;

// ===== 测试项目 =====
public class TestProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int MaterialCount { get; set; }
}

public class SaveTestProjectRequest
{
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Description { get; set; }
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
    public int? LocationId { get; set; }
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
    public string Name { get; set; } = "";
    public int ProjectId { get; set; }
    public string? VendorName { get; set; }
    public string? Model { get; set; }
    public string? Brand { get; set; }
    public int Quantity { get; set; } = 1;
    public int? DepartmentId { get; set; }
    public int? LocationId { get; set; }
    public int? CustodianId { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public List<string>? Images { get; set; }
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
    public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();
    public DateTime ApplyTime { get; set; }
    public DateTime Deadline { get; set; }
    /// <summary>当无需审批直接转移时为 true</summary>
    public bool DirectTransfer { get; set; }
}

public class MaterialFlowRecordDto
{
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
    public string? Reason { get; set; }
}

public class MaterialApprovalRequest
{
    public string? NodeId { get; set; }
    public string? Opinion { get; set; }
}

public class MaterialRejectRequest
{
    public string? NodeId { get; set; }
    public string Reason { get; set; } = "";
}
