namespace AssetManagement.Domain.Entities;

/// <summary>测试料件生命周期操作记录，不依赖人员流转审批单。</summary>
public class TestMaterialRecord
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public int? OperatorUserId { get; set; }
    public string Action { get; set; } = "";
    public string? Operator { get; set; }
    public string? Comment { get; set; }
    public DateTime OperatedAt { get; set; }
}
