using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/approvals")]
public class ApprovalController : ControllerBase
{
    private readonly IWorkflowService _service;

    public ApprovalController(IWorkflowService service)
    {
        _service = service;
    }

    [HttpPost]
    [HasPermission("approval:create")]
    public async Task<ApiResult<ApprovalFlowDto>> Start(StartApprovalRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.StartAsync(request, CurrentUserId()));

    [HttpGet("pending")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<List<ApprovalFlowDto>>> Pending()
        => ApiResult<List<ApprovalFlowDto>>.Ok(await _service.PendingAsync(CurrentUserId()));

    [HttpGet("pending-page")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<PagedResult<ApprovalFlowDto>>> PendingPage([FromQuery] ApprovalFlowPageQuery query)
        => ApiResult<PagedResult<ApprovalFlowDto>>.Ok(await _service.PendingPageAsync(CurrentUserId(), query));

    [HttpGet("handled-page")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<PagedResult<ApprovalFlowDto>>> HandledPage([FromQuery] ApprovalFlowPageQuery query)
        => ApiResult<PagedResult<ApprovalFlowDto>>.Ok(await _service.HandledPageAsync(CurrentUserId(), query));

    [HttpGet("mine")]
    [HasPermission("approval:view")]
    public async Task<ApiResult<List<ApprovalFlowDto>>> Mine()
        => ApiResult<List<ApprovalFlowDto>>.Ok(await _service.MineAsync(CurrentUserId()));

    [HttpGet("mine-page")]
    [HasPermission("approval:view")]
    public async Task<ApiResult<PagedResult<ApprovalFlowDto>>> MinePage([FromQuery] ApprovalFlowPageQuery query)
        => ApiResult<PagedResult<ApprovalFlowDto>>.Ok(await _service.MinePageAsync(CurrentUserId(), query));

    [HttpGet("pending-return")]
    [HasPermission("approval:confirm-return")]
    public async Task<ApiResult<List<ApprovalFlowDto>>> PendingReturn()
        => ApiResult<List<ApprovalFlowDto>>.Ok(await _service.PendingReturnsAsync(CurrentUserId()));

    [HttpGet("pending-return-page")]
    [HasPermission("approval:confirm-return")]
    public async Task<ApiResult<PagedResult<ApprovalFlowDto>>> PendingReturnPage([FromQuery] ApprovalFlowPageQuery query)
        => ApiResult<PagedResult<ApprovalFlowDto>>.Ok(await _service.PendingReturnsPageAsync(CurrentUserId(), query));

    [HttpGet("{id:int}")]
    [HasPermission("approval:view")]
    public async Task<ApiResult<ApprovalFlowDto>> Get(int id)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.GetFlowAsync(id, CurrentUserId()));

    [HttpPost("{id:int}/approve")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<ApprovalFlowDto>> Approve(int id, ApprovalActionRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.ApproveAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/reject")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<ApprovalFlowDto>> Reject(int id, RejectRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.RejectAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/withdraw")]
    [HasPermission("approval:view")]
    public async Task<ApiResult<ApprovalFlowDto>> Withdraw(int id)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.WithdrawAsync(id, CurrentUserId()));

    [HttpPost("{id:int}/add-sign")]
    [HasPermission("approval:add-sign")]
    public async Task<ApiResult<ApprovalFlowDto>> AddSign(int id, AddSignRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.AddSignAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/cancel-add-sign")]
    [HasPermission("approval:add-sign")]
    public async Task<ApiResult<ApprovalFlowDto>> CancelAddSign(int id, CancelAddSignRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.CancelAddSignAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/confirm-return")]
    [HasPermission("approval:confirm-return")]
    public async Task<ApiResult<ApprovalFlowDto>> ConfirmReturn(int id)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.ConfirmReturnAsync(id, CurrentUserId()));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
