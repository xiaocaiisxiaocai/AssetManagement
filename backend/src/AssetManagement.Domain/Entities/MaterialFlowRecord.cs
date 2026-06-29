namespace AssetManagement.Domain.Entities;

public class MaterialFlowRecord
{
    public int Id { get; set; }
    public int FlowId { get; set; }
    public string Action { get; set; } = "";
    public string? Operator { get; set; }
    public string? Comment { get; set; }
    public DateTime OperatedAt { get; set; }
}
