using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    public async Task<ApiResult<LoginResponse>> Login(LoginRequest request)
        => ApiResult<LoginResponse>.Ok(await _auth.LoginAsync(request));

    [HttpGet("user-info")]
    [Authorize]
    public async Task<ApiResult<UserInfoDto>> UserInfo()
        => ApiResult<UserInfoDto>.Ok(await _auth.GetUserInfoAsync(CurrentUserId()));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}

