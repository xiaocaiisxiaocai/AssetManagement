namespace AssetManagement.Domain.Entities;

public class MaterialFlowRecord
{
    public int Id { get; set; }
    public int FlowId { get; set; }
    public int? OperatorUserId { get; set; }
    public string? NodeId { get; set; }
    public string Action { get; set; } = "";
    public string? Operator { get; set; }
    public string? Comment { get; set; }
    public DateTime OperatedAt { get; set; }
}
