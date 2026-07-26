using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ApiResult<LoginResponse>> Login(LoginRequest request)
        => ApiResult<LoginResponse>.Ok(await _auth.LoginAsync(request));

    [HttpPost("logout")]
    [Authorize]
    public async Task<ApiResult<object?>> Logout()
    {
        await _auth.LogoutAsync(CurrentUserId());
        return ApiResult.Ok();
    }

    [HttpGet("user-info")]
    [Authorize]
    public async Task<ApiResult<UserInfoDto>> UserInfo()
        => ApiResult<UserInfoDto>.Ok(await _auth.GetUserInfoAsync(CurrentUserId()));

    [HttpPut("change-password")]
    [Authorize]
    [EnableRateLimiting("credential-change")]
    public async Task<ApiResult<object?>> ChangePassword(ChangePasswordRequest request)
    {
        await _auth.ChangePasswordAsync(CurrentUserId(), request);
        return ApiResult.Ok();
    }

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}

