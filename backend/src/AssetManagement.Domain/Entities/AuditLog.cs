namespace AssetManagement.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string ActionType { get; set; } = "";
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string Summary { get; set; } = "";
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime OccurredAt { get; set; }
}
