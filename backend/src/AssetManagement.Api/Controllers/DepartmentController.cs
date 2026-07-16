using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
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
    [HasPermission("department:view")]
    public async Task<ApiResult<List<DepartmentNodeDto>>> Tree()
        => ApiResult<List<DepartmentNodeDto>>.Ok(await _service.GetDepartmentTreeAsync());

    [HttpGet("levels")]
    [HasPermission("department:view")]
    public async Task<ApiResult<List<OrganizationLevelDto>>> Levels()
        => ApiResult<List<OrganizationLevelDto>>.Ok(await _service.GetOrganizationLevelsAsync());

    [HttpGet("options")]
    [Authorize]
    public async Task<ApiResult<List<DepartmentOptionDto>>> Options()
        => ApiResult<List<DepartmentOptionDto>>.Ok(ToOptions(await _service.GetDepartmentTreeAsync()));

    [HttpPost]
    [HasPermission("department:create")]
    public async Task<ApiResult<DepartmentNodeDto>> Create(CreateDepartmentRequest request)
        => ApiResult<DepartmentNodeDto>.Ok(await _service.CreateDepartmentAsync(request));

    [HttpPut("{id:int}")]
    [HasPermission("department:edit")]
    public async Task<ApiResult<DepartmentNodeDto>> Update(int id, UpdateDepartmentRequest request)
        => ApiResult<DepartmentNodeDto>.Ok(await _service.UpdateDepartmentAsync(id, request));

    [HttpDelete("{id:int}")]
    [HasPermission("department:delete")]
    public async Task<ApiResult<object?>> Delete(int id)
    {
        await _service.DeleteDepartmentAsync(id);
        return ApiResult.Ok();
    }

    private static List<DepartmentOptionDto> ToOptions(IEnumerable<DepartmentNodeDto> departments)
        => departments.Select(x => new DepartmentOptionDto
        {
            Id = x.Id,
            Name = x.Name,
            IsActive = x.IsActive,
            Children = ToOptions(x.Children)
        }).ToList();
}
