namespace AssetManagement.Domain.Entities;

public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Module { get; set; }
}
