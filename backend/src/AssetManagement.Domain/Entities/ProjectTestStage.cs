namespace AssetManagement.Domain.Entities;

public class ProjectTestStage
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = "";
    public string? Objective { get; set; }
    public string? TestContent { get; set; }
    public string? TestResult { get; set; }
    public string? Conclusion { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "planned";
    public bool IsDeleted { get; set; }
    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
