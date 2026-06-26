namespace AssetManagement.Domain.Entities;

public class TestProjectFollowup
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public DateTime DueDate { get; set; }
    public string Content { get; set; } = "";
    public int FilledById { get; set; }
    public DateTime FilledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
