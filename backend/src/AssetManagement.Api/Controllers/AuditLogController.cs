using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditQueryService _service;

    public AuditLogController(IAuditQueryService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission("admin:audit")]
    public async Task<ApiResult<PagedResult<AuditLogDto>>> List([FromQuery] AuditLogQuery query)
        => ApiResult<PagedResult<AuditLogDto>>.Ok(await _service.QueryAsync(query));

    [HttpGet("export")]
    [HasPermission("admin:audit")]
    public async Task<FileContentResult> Export([FromQuery] AuditLogQuery query)
        => File(await _service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "audit-logs.xlsx");
}
