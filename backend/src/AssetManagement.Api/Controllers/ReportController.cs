using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.Reports;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _service;

    public ReportController(IReportService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    [HasPermission("report:view")]
    public async Task<ApiResult<AssetSummaryDto>> Summary()
        => ApiResult<AssetSummaryDto>.Ok(await _service.GetSummaryAsync());

    [HttpGet("summary/export")]
    [HasPermission("report:view")]
    public async Task<FileContentResult> ExportSummary()
        => File(await _service.ExportSummaryAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "asset-summary.xlsx");

    [HttpGet("borrowed")]
    [HasPermission("report:view")]
    public async Task<ApiResult<PagedResult<BorrowReportRow>>> Borrowed([FromQuery] BorrowReportQuery query)
        => ApiResult<PagedResult<BorrowReportRow>>.Ok(await _service.QueryBorrowedAsync(query));

    [HttpGet("borrowed/export")]
    [HasPermission("report:view")]
    public async Task<FileContentResult> ExportBorrowed([FromQuery] BorrowReportQuery query)
        => File(await _service.ExportBorrowedAsync(query), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "borrowed-report.xlsx");

    [HttpGet("overdue")]
    [HasPermission("report:view")]
    public async Task<ApiResult<List<OverdueReportRow>>> Overdue()
        => ApiResult<List<OverdueReportRow>>.Ok(await _service.QueryOverdueAsync());

    [HttpGet("overdue/export")]
    [HasPermission("report:view")]
    public async Task<FileContentResult> ExportOverdue()
        => File(await _service.ExportOverdueAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "overdue-report.xlsx");

    [HttpPost("overdue/{assetId:int}/remind")]
    [HasPermission("report:view")]
    public async Task<ApiResult<object?>> Remind(int assetId)
    {
        await _service.RemindOverdueAsync(assetId, CurrentUserId());
        return ApiResult.Ok("已发送站内催办");
    }

    [HttpPost("overdue/remind-batch")]
    [HasPermission("report:view")]
    public async Task<ApiResult<object?>> RemindBatch(RemindBatchRequest request)
    {
        var count = await _service.RemindOverdueBatchAsync(request.AssetIds, CurrentUserId());
        return ApiResult.Ok($"已发送 {count} 条站内催办");
    }

    private int? CurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var id) ? id : null;
    }
}

public record RemindBatchRequest
{
    public List<int> AssetIds { get; init; } = new();
}
