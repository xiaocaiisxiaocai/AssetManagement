namespace AssetManagement.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string EmployeeNo { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int? DepartmentId { get; set; }
    public int? SupervisorId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public List<UserRole> UserRoles { get; set; } = new();
}
