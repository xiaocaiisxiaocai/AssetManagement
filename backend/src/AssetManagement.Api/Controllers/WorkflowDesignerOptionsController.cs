using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/workflow-designer/options")]
public sealed class WorkflowDesignerOptionsController : ControllerBase
{
    private readonly IRbacService _rbac;

    public WorkflowDesignerOptionsController(IRbacService rbac) => _rbac = rbac;

    [HttpGet]
    [HasPermission("workflow:design")]
    public async Task<ApiResult<WorkflowDesignerOptionsDto>> Get(
        string? keyword = null,
        int page = 1,
        int pageSize = 50)
        => ApiResult<WorkflowDesignerOptionsDto>.Ok(
            await _rbac.GetWorkflowDesignerOptionsAsync(keyword, page, pageSize));
}
