using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/test-projects/{projectId:int}/workspace")]
[HasPermission("project:view")]
public class ProjectWorkspaceController : ControllerBase
{
    private readonly IProjectWorkspaceService _service;
    public ProjectWorkspaceController(IProjectWorkspaceService service) => _service = service;

    [HttpGet]
    public async Task<ApiResult<ProjectWorkspaceDto>> Get(int projectId)
        => ApiResult<ProjectWorkspaceDto>.Ok(await _service.GetAsync(projectId, CurrentUserId()));

    [HttpPost("stages")]
    public async Task<ApiResult<ProjectTestStageDto>> CreateStage(int projectId, SaveProjectTestStageRequest request)
        => ApiResult<ProjectTestStageDto>.Ok(await _service.SaveStageAsync(projectId, null, request, CurrentUserId()));

    [HttpPut("stages/{stageId:int}")]
    public async Task<ApiResult<ProjectTestStageDto>> UpdateStage(int projectId, int stageId, SaveProjectTestStageRequest request)
        => ApiResult<ProjectTestStageDto>.Ok(await _service.SaveStageAsync(projectId, stageId, request, CurrentUserId()));

    [HttpDelete("stages/{stageId:int}")]
    public async Task<ApiResult<object?>> DeleteStage(int projectId, int stageId, [FromQuery] uint rowVersion)
    {
        await _service.DeleteStageAsync(projectId, stageId, rowVersion, CurrentUserId());
        return ApiResult.Ok();
    }

    [HttpPost("documents")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 22 * 1024 * 1024)]
    public async Task<ApiResult<ProjectReportDocumentDto>> Upload(int projectId, IFormFile file,
        [FromForm] int? stageId, [FromForm, StringLength(500)] string? description)
    {
        await using var stream = file.OpenReadStream();
        return ApiResult<ProjectReportDocumentDto>.Ok(await _service.UploadAsync(
            projectId, stageId, description, file.FileName, file.Length, stream, CurrentUserId()));
    }

    [HttpGet("documents/{documentId:int}/download")]
    public async Task<FileStreamResult> Download(int projectId, int documentId)
    {
        var file = await _service.DownloadAsync(projectId, documentId, CurrentUserId());
        return File(file.Stream, file.ContentType, file.FileName);
    }

    [HttpDelete("documents/{documentId:int}")]
    public async Task<ApiResult<object?>> DeleteDocument(int projectId, int documentId)
    {
        await _service.DeleteDocumentAsync(projectId, documentId, CurrentUserId());
        return ApiResult.Ok();
    }

    [HttpGet("report")]
    public Task<FileStreamResult> Report(int projectId, [FromQuery] int? stageId)
        => Export(projectId, stageId, false);

    [HttpGet("package")]
    public Task<FileStreamResult> Package(int projectId, [FromQuery] int? stageId)
        => Export(projectId, stageId, true);

    private async Task<FileStreamResult> Export(int projectId, int? stageId, bool package)
    {
        var file = await _service.ExportAsync(projectId, stageId, package, CurrentUserId());
        return File(file.Stream, file.ContentType, file.FileName);
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
