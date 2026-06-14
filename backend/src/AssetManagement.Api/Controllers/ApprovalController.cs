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
    [HasPermission("approval:view")]
    public async Task<ApiResult<ApprovalFlowDto>> Start(StartApprovalRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.StartAsync(request, CurrentUserId()));

    [HttpGet("pending")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<List<ApprovalFlowDto>>> Pending()
        => ApiResult<List<ApprovalFlowDto>>.Ok(await _service.PendingAsync(CurrentUserId()));

    [HttpGet("mine")]
    [HasPermission("approval:view")]
    public async Task<ApiResult<List<ApprovalFlowDto>>> Mine()
        => ApiResult<List<ApprovalFlowDto>>.Ok(await _service.MineAsync(CurrentUserId()));

    [HttpGet("{id:int}")]
    [HasPermission("approval:view")]
    public async Task<ApiResult<ApprovalFlowDto>> Get(int id)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.GetFlowAsync(id));

    [HttpPost("{id:int}/approve")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<ApprovalFlowDto>> Approve(int id, ApprovalActionRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.ApproveAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/reject")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<ApprovalFlowDto>> Reject(int id, RejectRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.RejectAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/add-sign")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<ApprovalFlowDto>> AddSign(int id, AddSignRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.AddSignAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/transfer-sign")]
    [HasPermission("approval:handle")]
    public async Task<ApiResult<ApprovalFlowDto>> TransferSign(int id, TransferSignRequest request)
        => ApiResult<ApprovalFlowDto>.Ok(await _service.TransferSignAsync(id, request, CurrentUserId()));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
