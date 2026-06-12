namespace AssetManagement.Application.Rbac;

public record UserDto
{
    public int Id { get; init; }
    public string EmployeeNo { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
    public int[] RoleIds { get; init; } = Array.Empty<int>();
}

public record CreateUserRequest
{
    public string EmployeeNo { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public int[] RoleIds { get; init; } = Array.Empty<int>();
}

public record UpdateUserRequest
{
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public int[] RoleIds { get; init; } = Array.Empty<int>();
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

public record PermissionDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Module { get; init; }
}

public record MenuDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Path { get; init; }
    public string? Component { get; init; }
    public string? Icon { get; init; }
    public int Sort { get; init; }
    public string Type { get; init; } = "menu";
    public string? PermissionCode { get; init; }
    public List<MenuDto> Children { get; init; } = new();
}

