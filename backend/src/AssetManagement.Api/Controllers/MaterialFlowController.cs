using System.Security.Claims;
using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/material-flows")]
public class MaterialFlowController : ControllerBase
{
    private readonly IMaterialFlowService _service;
    public MaterialFlowController(IMaterialFlowService service) => _service = service;

    [HttpPost]
    [HasPermission("material-flow:transfer")]
    public async Task<ApiResult<MaterialFlowDto>> Initiate(InitiateTransferRequest request)
        => ApiResult<MaterialFlowDto>.Ok(await _service.InitiateTransferAsync(request, CurrentUserId()));

    [HttpGet("pending")]
    [HasPermission("material-flow:approve")]
    public async Task<ApiResult<List<MaterialFlowDto>>> Pending([FromQuery] int? projectId = null)
        => ApiResult<List<MaterialFlowDto>>.Ok(await _service.PendingAsync(CurrentUserId(), projectId));

    [HttpGet("mine")]
    [HasPermission("material-flow:view")]
    public async Task<ApiResult<List<MaterialFlowDto>>> Mine([FromQuery] int? projectId = null)
        => ApiResult<List<MaterialFlowDto>>.Ok(await _service.MineAsync(CurrentUserId(), projectId));

    [HttpGet("{id:int}")]
    [HasPermission("material-flow:view")]
    public async Task<ApiResult<MaterialFlowDto>> Get(int id)
        => ApiResult<MaterialFlowDto>.Ok(await _service.GetAsync(id, CurrentUserId()));

    [HttpPost("{id:int}/approve")]
    [HasPermission("material-flow:approve")]
    public async Task<ApiResult<MaterialFlowDto>> Approve(int id, MaterialApprovalRequest request)
        => ApiResult<MaterialFlowDto>.Ok(await _service.ApproveAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/reject")]
    [HasPermission("material-flow:approve")]
    public async Task<ApiResult<MaterialFlowDto>> Reject(int id, MaterialRejectRequest request)
        => ApiResult<MaterialFlowDto>.Ok(await _service.RejectAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/withdraw")]
    [HasPermission("material-flow:view")]
    public async Task<ApiResult<MaterialFlowDto>> Withdraw(int id)
        => ApiResult<MaterialFlowDto>.Ok(await _service.WithdrawAsync(id, CurrentUserId()));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
