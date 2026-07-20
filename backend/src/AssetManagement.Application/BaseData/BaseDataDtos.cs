namespace AssetManagement.Application.BaseData;

using System.ComponentModel.DataAnnotations;

public record DepartmentNodeDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string? OrganizationLevelCode { get; init; }
    public string? OrganizationLevelName { get; init; }
    public string Name { get; init; } = "";
    public int? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public int AssetCount { get; init; }
    public bool IsActive { get; init; }
    public List<DepartmentNodeDto> Children { get; init; } = new();
}

public record CreateDepartmentRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public int? ManagerId { get; init; }
    public string? OrganizationLevelCode { get; init; }
}

public record DepartmentOptionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public bool IsActive { get; init; }
    public List<DepartmentOptionDto> Children { get; init; } = new();
}

public record OrganizationLevelDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int Sort { get; init; }
    public bool IsActive { get; init; }
}

public record UpdateDepartmentRequest
{
    public int? ParentId { get; init; }
    public string Name { get; init; } = "";
    public int? ManagerId { get; init; }
    public string? OrganizationLevelCode { get; init; }
    public bool IsActive { get; init; } = true;
}

public record CategoryNodeDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string CodeSeg { get; init; } = "";
    public string Code { get; init; } = "";
    public string? Remark { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public List<CategoryNodeDto> Children { get; init; } = new();
}

public record CreateCategoryRequest
{
    public int? ParentId { get; init; }
    public string CodeSeg { get; init; } = "";
    [MaxLength(500)]
    public string? Remark { get; init; }
}

public record UpdateCategoryRequest
{
    public int? ParentId { get; init; }
    public string CodeSeg { get; init; } = "";
    [MaxLength(500)]
    public string? Remark { get; init; }
}

public record LocationNodeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

public record CreateLocationRequest
{
    public string Name { get; init; } = "";
}

public record UpdateLocationRequest
{
    public string Name { get; init; } = "";
}

public record SystemSettingDto
{
    public int Id { get; init; }
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public string? Description { get; init; }
}

public record RuntimeSettingsDto
{
    public int PageSize { get; init; }
    public int AttachmentMaxMb { get; init; }
    public List<string> AssetConditionOptions { get; init; } = new();
    public CategoryCodeRulesDto CategoryCodeRules { get; init; } = new();
}

public record CategoryCodeRuleDto
{
    public string Length { get; init; } = "";
    public string Regex { get; init; } = "";
}

public record CategoryCodeRulesDto
{
    public CategoryCodeRuleDto Level1 { get; init; } = new();
    public CategoryCodeRuleDto Level2 { get; init; } = new();
    public CategoryCodeRuleDto Level3 { get; init; } = new();
}

public record SaveSystemSettingRequest
{
    [Required(ErrorMessage = "系统参数键不能为空"), MaxLength(100)]
    public string Key { get; init; } = "";
    [Required(ErrorMessage = "系统参数值不能为空")]
    public string Value { get; init; } = "";
    [MaxLength(200)]
    public string? Description { get; init; }
}
