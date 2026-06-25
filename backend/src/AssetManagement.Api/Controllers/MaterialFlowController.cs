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
    [HasPermission("material:transfer")]
    public async Task<ApiResult<MaterialFlowDto>> Initiate(InitiateTransferRequest request)
        => ApiResult<MaterialFlowDto>.Ok(await _service.InitiateTransferAsync(request, CurrentUserId()));

    [HttpGet("pending")]
    [HasPermission("material:approve")]
    public async Task<ApiResult<List<MaterialFlowDto>>> Pending()
        => ApiResult<List<MaterialFlowDto>>.Ok(await _service.PendingAsync(CurrentUserId()));

    [HttpGet("mine")]
    [HasPermission("material:transfer")]
    public async Task<ApiResult<List<MaterialFlowDto>>> Mine()
        => ApiResult<List<MaterialFlowDto>>.Ok(await _service.MineAsync(CurrentUserId()));

    [HttpGet("{id:int}")]
    [HasPermission("material:view")]
    public async Task<ApiResult<MaterialFlowDto>> Get(int id)
        => ApiResult<MaterialFlowDto>.Ok(await _service.GetAsync(id));

    [HttpPost("{id:int}/approve")]
    [HasPermission("material:approve")]
    public async Task<ApiResult<MaterialFlowDto>> Approve(int id, MaterialApprovalRequest request)
        => ApiResult<MaterialFlowDto>.Ok(await _service.ApproveAsync(id, request, CurrentUserId()));

    [HttpPost("{id:int}/reject")]
    [HasPermission("material:approve")]
    public async Task<ApiResult<MaterialFlowDto>> Reject(int id, MaterialRejectRequest request)
        => ApiResult<MaterialFlowDto>.Ok(await _service.RejectAsync(id, request, CurrentUserId()));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
