using AssetManagement.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public ApiResult<string> Get() => ApiResult<string>.Ok("healthy");
}
