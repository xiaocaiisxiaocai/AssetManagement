namespace AssetManagement.Domain.Entities;

public class TestProject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? ProjectTypeCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? PlannedFinishDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? ProgressCode { get; set; }
    public int? OwnerId { get; set; }
    public string? TestStatus { get; set; }
    public int FollowUpIntervalDays { get; set; } = 14;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public uint RowVersion { get; set; }
}
