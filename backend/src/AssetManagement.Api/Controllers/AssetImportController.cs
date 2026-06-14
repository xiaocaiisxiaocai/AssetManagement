using AssetManagement.Application.Assets;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/assets/import")]
public class AssetImportController : ControllerBase
{
    private readonly IAssetService _service;

    public AssetImportController(IAssetService service)
    {
        _service = service;
    }

    [HttpGet("template")]
    [HasPermission("asset:view")]
    public FileContentResult Template()
        => File(_service.BuildImportTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "asset-import-template.xlsx");

    [HttpPost("validate")]
    [HasPermission("asset:create")]
    public async Task<ApiResult<List<ImportPreviewRow>>> Validate(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return ApiResult<List<ImportPreviewRow>>.Ok(await _service.ValidateImportAsync(stream));
    }

    [HttpPost("confirm")]
    [HasPermission("asset:create")]
    public async Task<ApiResult<ImportConfirmResult>> Confirm(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return ApiResult<ImportConfirmResult>.Ok(await _service.ConfirmImportAsync(stream));
    }
}
