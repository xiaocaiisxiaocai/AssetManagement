using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingController : ControllerBase
{
    private readonly IBaseDataService _service;

    public SettingController(IBaseDataService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission("setting:view")]
    public async Task<ApiResult<List<SystemSettingDto>>> List()
        => ApiResult<List<SystemSettingDto>>.Ok(await _service.GetSettingsAsync());

    [HttpGet("runtime")]
    [Authorize]
    public async Task<ApiResult<RuntimeSettingsDto>> Runtime()
        => ApiResult<RuntimeSettingsDto>.Ok(await _service.GetRuntimeSettingsAsync());

    [HttpPut]
    [HasPermission("setting:edit")]
    public async Task<ApiResult<List<SystemSettingDto>>> Save(List<SaveSystemSettingRequest> requests)
        => ApiResult<List<SystemSettingDto>>.Ok(await _service.SaveSettingsAsync(requests));
}
