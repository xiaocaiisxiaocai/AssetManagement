namespace AssetManagement.Domain.Entities;

public class TestProjectOption
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}
