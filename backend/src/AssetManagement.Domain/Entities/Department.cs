namespace AssetManagement.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}
