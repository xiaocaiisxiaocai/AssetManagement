using AssetManagement.Application.Assets;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/assets/overview")]
public class AssetOverviewController : ControllerBase
{
    private readonly IAssetOverviewService _service;

    public AssetOverviewController(IAssetOverviewService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission("asset:view")]
    public async Task<ApiResult<AssetOverviewDto>> Get()
        => ApiResult<AssetOverviewDto>.Ok(await _service.GetOverviewAsync());
}
