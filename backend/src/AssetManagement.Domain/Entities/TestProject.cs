namespace AssetManagement.Domain.Entities;

public class TestProject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
