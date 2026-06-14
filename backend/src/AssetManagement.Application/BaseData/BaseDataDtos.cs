namespace AssetManagement.Application.BaseData;

public record DepartmentNodeDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string Code { get; init; } = "";
    public string? ManagerName { get; init; }
    public int AssetCount { get; init; }
    public bool IsActive { get; init; }
    public List<DepartmentNodeDto> Children { get; init; } = new();
}

public record CreateDepartmentRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string Code { get; init; } = "";
    public int? ManagerId { get; init; }
}

public record UpdateDepartmentRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string Code { get; init; } = "";
    public int? ManagerId { get; init; }
    public bool IsActive { get; init; } = true;
}

public record CategoryNodeDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string CodeSeg { get; init; } = "";
    public string Code { get; init; } = "";
    public List<CategoryNodeDto> Children { get; init; } = new();
}

public record CreateCategoryRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string CodeSeg { get; init; } = "";
}

public record UpdateCategoryRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string CodeSeg { get; init; } = "";
}

public record LocationNodeDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string? QrCode { get; init; }
    public List<LocationNodeDto> Children { get; init; } = new();
}

public record CreateLocationRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string? QrCode { get; init; }
}

public record UpdateLocationRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string? QrCode { get; init; }
}

public record SystemSettingDto
{
    public int Id { get; init; }
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public string? Description { get; init; }
}

public record SaveSystemSettingRequest
{
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public string? Description { get; init; }
}
