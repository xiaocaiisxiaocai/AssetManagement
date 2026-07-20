using System.ComponentModel.DataAnnotations;
using AssetManagement.Application.Common;

namespace AssetManagement.Application.Rbac;

public record UserDto
{
    public int Id { get; init; }
    public string EmployeeNo { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? SupervisorId { get; init; }
    public string? SupervisorName { get; init; }
    public int[] RoleIds { get; init; } = Array.Empty<int>();
    public string[] RoleNames { get; init; } = Array.Empty<string>();
}

public record UserOptionDto
{
    public int Id { get; init; }
    public string EmployeeNo { get; init; } = "";
    public string Name { get; init; } = "";
    public string? DepartmentName { get; init; }
}

public record WorkflowDesignerRoleOptionDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public record WorkflowDesignerDepartmentOptionDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string? OrganizationLevelCode { get; init; }
}

public record WorkflowDesignerOrganizationLevelOptionDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public record WorkflowDesignerOptionsDto
{
    public PagedResult<UserOptionDto> Users { get; init; } = new();
    public List<WorkflowDesignerRoleOptionDto> Roles { get; init; } = new();
    public List<WorkflowDesignerDepartmentOptionDto> Departments { get; init; } = new();
    public List<WorkflowDesignerOrganizationLevelOptionDto> OrganizationLevels { get; init; } = new();
}

public record CreateUserRequest
{
    [Required, StringLength(50)]
    public string EmployeeNo { get; init; } = "";
    [Required, StringLength(100)]
    public string Name { get; init; } = "";
    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }
    [StringLength(50)]
    public string? Phone { get; init; }
    public string? Password { get; init; }
    public int? DepartmentId { get; init; }
    public int? SupervisorId { get; init; }
    public int[] RoleIds { get; init; } = Array.Empty<int>();
}

public record UpdateUserRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = "";
    [EmailAddress, StringLength(200)]
    public string? Email { get; init; }
    [StringLength(50)]
    public string? Phone { get; init; }
    public int? DepartmentId { get; init; }
    public int? SupervisorId { get; init; }
    public int[] RoleIds { get; init; } = Array.Empty<int>();
}

public record SetUserStatusRequest
{
    public bool? IsActive { get; init; }
}

public record UserImportRowDto
{
    public int Row { get; init; }
    public string EmployeeNo { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? DepartmentName { get; init; }
    public string RoleName { get; init; } = "";
    public bool IsValid { get; init; }
    public string Error { get; init; } = "";
}

public record UserImportResultDto
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<UserImportRowDto> Rows { get; init; } = new();
}

public record RoleDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public int[] PermissionIds { get; init; } = Array.Empty<int>();
    public int[] MenuIds { get; init; } = Array.Empty<int>();
}

/// <summary>创建角色时只允许设置角色自身属性；权限和菜单必须走独立授权接口。</summary>
public record CreateRoleRequest
{
    [Required, StringLength(50)]
    public string Code { get; init; } = "";

    [Required, StringLength(100)]
    public string Name { get; init; } = "";

    public bool IsActive { get; init; } = true;
}

public record UpdateRoleRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = "";

    public bool IsActive { get; init; } = true;
}

public record SetRolePermissionsRequest
{
    public int[] PermissionIds { get; init; } = Array.Empty<int>();
}

public record SetRoleMenusRequest
{
    public int[] MenuIds { get; init; } = Array.Empty<int>();
}

public record SetRoleAccessRequest
{
    public int[] PermissionIds { get; init; } = Array.Empty<int>();
    public int[] MenuIds { get; init; } = Array.Empty<int>();
}

public record PermissionDto
{
    public int Id { get; init; }
    [Required, StringLength(100)]
    public string Code { get; init; } = "";
    [Required, StringLength(100)]
    public string Name { get; init; } = "";
    [StringLength(50)]
    public string? Module { get; init; }
}

public record MenuDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    [Required, StringLength(100)]
    public string Name { get; init; } = "";
    [Required, StringLength(100)]
    public string Title { get; init; } = "";
    [StringLength(200)]
    public string? Path { get; init; }
    [StringLength(200)]
    public string? Component { get; init; }
    [StringLength(100)]
    public string? Icon { get; init; }
    public int Sort { get; init; }
    public string Type { get; init; } = "menu";
    [StringLength(100)]
    public string? PermissionCode { get; init; }
    public List<MenuDto> Children { get; init; } = new();
}

