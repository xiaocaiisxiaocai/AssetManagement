using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    /// <summary>
    /// 默认返回全组织部门，供筛选和跨部门人员维护使用。
    /// scope=assignable 时只返回当前部门主管可把料件归属到的本部门及子部门。
    /// </summary>
    [HttpGet("options")]
    [Authorize]
    public async Task<ApiResult<List<DepartmentOptionDto>>> Options(string? scope = null)
    {
        var tree = await _service.GetDepartmentTreeAsync();
        if (string.Equals(scope, "assignable", StringComparison.OrdinalIgnoreCase)
            && User.IsInRole("supervisor") && !User.IsInRole("admin"))
        {
            if (!int.TryParse(User.FindFirstValue("departmentId"), out var departmentId))
            {
                return ApiResult<List<DepartmentOptionDto>>.Ok([]);
            }

            var scoped = FindSubtree(tree, departmentId);
            tree = scoped is null ? [] : [scoped];
        }

        return ApiResult<List<DepartmentOptionDto>>.Ok(ToOptions(tree));
    }

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

    private static DepartmentNodeDto? FindSubtree(IEnumerable<DepartmentNodeDto> nodes, int id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            var child = FindSubtree(node.Children, id);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static List<DepartmentOptionDto> ToOptions(IEnumerable<DepartmentNodeDto> departments)
        => departments.Select(x => new DepartmentOptionDto
        {
            Id = x.Id,
            Name = x.Name,
            ManagerId = x.ManagerId,
            ManagerName = x.ManagerName,
            IsActive = x.IsActive,
            Children = ToOptions(x.Children)
        }).ToList();
}
