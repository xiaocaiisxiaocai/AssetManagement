using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditQueryService _service;
    private readonly IAuditMaintenanceService _maintenanceService;
    private readonly IDatabaseBackupService _backupService;

    public AuditLogController(
        IAuditQueryService service,
        IAuditMaintenanceService maintenanceService,
        IDatabaseBackupService backupService)
    {
        _service = service;
        _maintenanceService = maintenanceService;
        _backupService = backupService;
    }

    [HttpGet]
    [HasPermission("audit:view")]
    public async Task<ApiResult<PagedResult<AuditLogDto>>> List([FromQuery] AuditLogQuery query)
        => ApiResult<PagedResult<AuditLogDto>>.Ok(await _service.QueryAsync(query));

    [HttpGet("export")]
    [HasPermission("audit:export")]
    public async Task<FileContentResult> Export([FromQuery] AuditLogQuery query)
        => File(await _service.ExportAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "audit-logs.xlsx");

    [HttpGet("cleanup-preview")]
    [HasPermission("audit:cleanup")]
    public async Task<ApiResult<AuditCleanupPreviewDto>> CleanupPreview([FromQuery] int retentionDays)
        => ApiResult<AuditCleanupPreviewDto>.Ok(await _maintenanceService.PreviewCleanupAsync(retentionDays));

    [HttpDelete]
    [HasPermission("audit:cleanup")]
    public async Task<ApiResult<AuditCleanupResultDto>> Cleanup([FromQuery] int retentionDays)
        => ApiResult<AuditCleanupResultDto>.Ok(await _maintenanceService.CleanupAsync(retentionDays, CurrentUserId()));

    [HttpPost("database-backup")]
    [HasPermission("backup:manage")]
    public async Task<ApiResult<DatabaseBackupResultDto>> Backup(CancellationToken cancellationToken)
        => ApiResult<DatabaseBackupResultDto>.Ok(await _backupService.BackupAsync(cancellationToken));

    private int? CurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var value) ? value : null;
    }
}
