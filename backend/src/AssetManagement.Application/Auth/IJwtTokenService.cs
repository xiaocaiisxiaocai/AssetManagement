namespace AssetManagement.Application.Auth;

public interface IJwtTokenService
{
    string Create(int userId, string employeeNo, IEnumerable<string> permissionCodes, IEnumerable<string> roles);
}

