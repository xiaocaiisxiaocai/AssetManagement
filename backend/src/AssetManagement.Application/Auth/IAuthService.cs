namespace AssetManagement.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<UserInfoDto> GetUserInfoAsync(int userId);
    Task<List<RouteDto>> GetRoutesAsync(int userId);
}
