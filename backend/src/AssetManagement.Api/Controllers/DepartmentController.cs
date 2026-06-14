using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers;

[ApiController]
[Route("api/departments")]
public class DepartmentController : ControllerBase
{
    private readonly IBaseDataService _service;

    public DepartmentController(IBaseDataService service)
    {
        _service = service;
    }

    [HttpGet("tree")]
    [HasPermission("admin:user")]
    public async Task<ApiResult<List<DepartmentNodeDto>>> Tree()
        => ApiResult<List<DepartmentNodeDto>>.Ok(await _service.GetDepartmentTreeAsync());

    [HttpPost]
    [HasPermission("admin:user")]
    public async Task<ApiResult<DepartmentNodeDto>> Create(CreateDepartmentRequest request)
        => ApiResult<DepartmentNodeDto>.Ok(await _service.CreateDepartmentAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("admin:user")]
    public async Task<ApiResult<DepartmentNodeDto>> Update(int id, UpdateDepartmentRequest request)
        => ApiResult<DepartmentNodeDto>.Ok(await _service.UpdateDepartmentAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("admin:user")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteDepartmentAsync(id);
        return ApiResult.Ok();
    }
}
