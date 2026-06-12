namespace AssetManagement.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<RolePermission> RolePermissions { get; set; } = new();
    public List<RoleMenu> RoleMenus { get; set; } = new();
}
