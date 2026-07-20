using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.Auth;

public record LoginRequest
{
    [Required, StringLength(50)]
    public string EmployeeNo { get; init; } = "";
    [Required, StringLength(128)]
    public string Password { get; init; } = "";
}

public record LoginResponse
{
    public string Token { get; init; } = "";
}

public record ChangePasswordRequest
{
    [Required, StringLength(128)]
    public string OldPassword { get; init; } = "";
    [Required, StringLength(128)]
    public string NewPassword { get; init; } = "";
}

