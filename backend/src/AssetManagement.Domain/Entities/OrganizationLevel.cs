namespace AssetManagement.Domain.Entities;

public class OrganizationLevel
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}
