namespace AssetManagement.Application.Auth;

public record UserInfoDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string EmployeeNo { get; init; } = "";
    public string[] Roles { get; init; } = Array.Empty<string>();
    public string[] Permissions { get; init; } = Array.Empty<string>();
}

