namespace AssetManagement.Domain.Entities;

public class Menu
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public int Sort { get; set; }
    public string Type { get; set; } = "menu";
    public string? PermissionCode { get; set; }
}
