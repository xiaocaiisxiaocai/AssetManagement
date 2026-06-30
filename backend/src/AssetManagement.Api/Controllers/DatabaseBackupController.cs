using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/database-backups")]
public class DatabaseBackupController : ControllerBase
{
    private readonly IDatabaseBackupService _backupService;

    public DatabaseBackupController(IDatabaseBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet]
    [HasPermission("backup:manage")]
    public async Task<ApiResult<List<DatabaseBackupFileDto>>> List(CancellationToken cancellationToken)
        => ApiResult<List<DatabaseBackupFileDto>>.Ok(await _backupService.ListAsync(cancellationToken));

    [HttpPost]
    [HasPermission("backup:manage")]
    public async Task<ApiResult<DatabaseBackupResultDto>> Backup(CancellationToken cancellationToken)
        => ApiResult<DatabaseBackupResultDto>.Ok(await _backupService.BackupAsync(cancellationToken));

    [HttpGet("{fileName}/download")]
    [HasPermission("backup:manage")]
    public async Task<IActionResult> Download(string fileName, CancellationToken cancellationToken)
    {
        var file = await _backupService.OpenAsync(fileName, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Stream, file.ContentType, file.FileName);
    }
}
