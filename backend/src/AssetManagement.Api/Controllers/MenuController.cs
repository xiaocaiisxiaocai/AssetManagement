using System.Security.Claims;
using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/menu")]
public class MenuController : ControllerBase
{
    private readonly IAuthService _auth;

    public MenuController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpGet("routes")]
    [Authorize]
    public async Task<ApiResult<List<RouteDto>>> Routes()
        => ApiResult<List<RouteDto>>.Ok(await _auth.GetRoutesAsync(CurrentUserId()));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
