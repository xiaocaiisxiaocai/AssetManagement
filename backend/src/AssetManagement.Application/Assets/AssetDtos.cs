using AssetManagement.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.Assets;

public record AssetDto
{
    public int Id { get; init; }
    public string AssetNo { get; init; } = "";
    public string Name { get; init; } = "";
    public int CategoryId { get; init; }
    public string CategoryCode { get; init; } = "";
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public string? LocationName { get; init; }
    public int? CustodianId { get; init; }
    public string? CustodianName { get; init; }
    /// <summary>当前调用人是否处于该资产的部门管理范围；具体操作仍需对应权限码。</summary>
    public bool CanManage { get; init; }
    /// <summary>当前借用周期的原应归还日期；转让不会改变该日期。</summary>
    public string? ReturnDate { get; init; }
    public int Quantity { get; init; }
    public AssetStatus Status { get; init; }
    public DateTime? PurchaseDate { get; init; }
    public DateTime? RegistrationTime { get; init; }
    public string? CurrentCondition { get; init; }
    public string? Remark { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public List<string> Images { get; init; } = new();
}

public record AssetQuery
{
    public string? Keyword { get; init; }
    public string? AssetNo { get; init; }
    public string? Name { get; init; }
    public int? CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    public int? CustodianId { get; init; }
    public int? ExcludeCustodianId { get; init; }
    public AssetStatus? Status { get; init; }
    public string? DeleteStatus { get; init; }
    public bool DeletedOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record CreateAssetRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = "";
    public int CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    [StringLength(100)]
    public string? LocationName { get; init; }
    public int? CustodianId { get; init; }
    public int Quantity { get; init; } = 1;
    public DateTime? PurchaseDate { get; init; }
    public DateTime? RegistrationTime { get; init; }
    public string? CurrentCondition { get; init; }
    [StringLength(500)]
    public string? Remark { get; init; }
    public List<string>? Images { get; init; }
}

public record UpdateAssetRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = "";
    public int CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    [StringLength(100)]
    public string? LocationName { get; init; }
    public int? CustodianId { get; init; }
    public int Quantity { get; init; } = 1;
    public AssetStatus Status { get; init; } = AssetStatus.Available;
    public DateTime? PurchaseDate { get; init; }
    public DateTime? RegistrationTime { get; init; }
    public string? CurrentCondition { get; init; }
    [StringLength(500)]
    public string? Remark { get; init; }
    public List<string>? Images { get; init; }
}

public record ImportPreviewRow
{
    public int Row { get; init; }
    public string Name { get; init; } = "";
    public string CategoryCode { get; init; } = "";
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? CustodianId { get; init; }
    public string? CustodianEmployeeNo { get; init; }
    public string? CustodianName { get; init; }
    public string? LocationName { get; init; }
    public int Quantity { get; init; } = 1;
    public DateTime? PurchaseDate { get; init; }
    public DateTime? RegistrationTime { get; init; }
    public string? CurrentCondition { get; init; }
    public string? Remark { get; init; }
    public bool IsValid { get; init; }
    public string Error { get; init; } = "";
}

public record ImportConfirmResult
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<ImportPreviewRow> Rows { get; init; } = new();
}

/// <summary>资产详情:基本信息 + 流转历史 + 最近操作日志</summary>
public record AssetDetailDto
{
    public AssetDto Asset { get; init; } = new();
    public int? InitialCustodianId { get; init; }
    public string? InitialCustodianName { get; init; }
    public List<AssetFlowDto> Flows { get; init; } = new();
    public List<AssetAuditLogDto> RecentLogs { get; init; } = new();
}

/// <summary>资产详情页可见的最小审计信息，不包含请求详情、IP 和 User-Agent。</summary>
public record AssetAuditLogDto
{
    public int Id { get; init; }
    public int? UserId { get; init; }
    public string? UserName { get; init; }
    public string ActionType { get; init; } = "";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string Summary { get; init; } = "";
    public DateTime OccurredAt { get; init; }
}

/// <summary>资产流转时间线条目(借出/归还/转让等审批单的精简视图)</summary>
public record AssetFlowDto
{
    public int Id { get; init; }
    public string FlowNo { get; init; } = "";
    public string BizType { get; init; } = "";
    public string Status { get; init; } = "";
    public string Applicant { get; init; } = "";
    public string? Transferee { get; init; }
    public string? Reason { get; init; }
    public string? OriginalReturnDate { get; init; }
    public string? ReturnDate { get; init; }
    public DateTime ApplyTime { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? WithdrawnAt { get; init; }
}
