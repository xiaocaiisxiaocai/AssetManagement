namespace AssetManagement.Domain.Entities;

public class FlowRecord
{
    public int Id { get; set; }
    public int FlowId { get; set; }
    public string Action { get; set; } = "";
    public string? Operator { get; set; }
    public string? Comment { get; set; }
    public DateTime OperatedAt { get; set; }
}
