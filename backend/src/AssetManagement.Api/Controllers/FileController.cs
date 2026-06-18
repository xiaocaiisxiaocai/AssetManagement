using AssetManagement.Application.Common;
using AssetManagement.Application.Files;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FileController : ControllerBase
{
    private readonly IFileStorageService _storage;

    public FileController(IFileStorageService storage)
    {
        _storage = storage;
    }

    [HttpPost("upload")]
    [HasPermission("asset:edit")]
    public async Task<ApiResult<FileUploadResult>> Upload(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return ApiResult<FileUploadResult>.Ok(await _storage.SaveImageAsync(stream, file.FileName, file.Length));
    }

    [HttpGet("{name}")]
    [HasPermission("asset:view")]
    public IActionResult Get(string name)
    {
        var stored = _storage.Open(name);
        if (stored is null)
        {
            return NotFound();
        }
        return File(stored.Stream, stored.ContentType);
    }
}
