using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _service;

    public WorkflowController(IWorkflowService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission("workflow:design")]
    public async Task<ApiResult<List<WorkflowDto>>> List()
        => ApiResult<List<WorkflowDto>>.Ok(await _service.GetWorkflowsAsync());

    [HttpGet("{id:int}")]
    [HasPermission("workflow:design")]
    public async Task<ApiResult<WorkflowDto>> Get(int id)
        => ApiResult<WorkflowDto>.Ok(await _service.GetWorkflowAsync(id));

    [HttpPost]
    [HasPermission("workflow:design")]
    public async Task<ApiResult<WorkflowDto>> Create(SaveWorkflowRequest request)
        => ApiResult<WorkflowDto>.Ok(await _service.CreateWorkflowAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("workflow:design")]
    public async Task<ApiResult<WorkflowDto>> Save(int id, SaveWorkflowRequest request)
        => ApiResult<WorkflowDto>.Ok(await _service.SaveWorkflowAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("workflow:design")]
    public async Task<ApiResult<bool>> Delete(int id)
    {
        await _service.DeleteWorkflowAsync(id);
        return ApiResult<bool>.Ok(true);
    }
}
