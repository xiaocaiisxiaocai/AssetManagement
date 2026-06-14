using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
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
    [HasPermission("admin:user")]
    public async Task<ApiResult<List<SystemSettingDto>>> List()
        => ApiResult<List<SystemSettingDto>>.Ok(await _service.GetSettingsAsync());

    [HttpPut]
    [HasPermission("admin:user")]
    public async Task<ApiResult<List<SystemSettingDto>>> Save(List<SaveSystemSettingRequest> requests)
        => ApiResult<List<SystemSettingDto>>.Ok(await _service.SaveSettingsAsync(requests));
}
