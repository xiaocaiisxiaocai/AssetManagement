namespace AssetManagement.Application.Auth;

public record LoginRequest
{
    public string EmployeeNo { get; init; } = "";
    public string Password { get; init; } = "";
}

public record LoginResponse
{
    public string Token { get; init; } = "";
}

public record ChangePasswordRequest
{
    public string OldPassword { get; init; } = "";
    public string NewPassword { get; init; } = "";
}

