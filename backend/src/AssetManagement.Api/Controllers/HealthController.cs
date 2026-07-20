using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db) => _db = db;

    [HttpGet("live")]
    public ApiResult<string> Live() => ApiResult<string>.Ok("healthy");

    [HttpGet]
    [HttpGet("ready")]
    public async Task<ActionResult<ApiResult<string>>> Ready(CancellationToken cancellationToken)
    {
        if (!await _db.Database.CanConnectAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResult<string>.Fail(5030, "database unavailable"));
        }

        return Ok(ApiResult<string>.Ok("healthy"));
    }
}
