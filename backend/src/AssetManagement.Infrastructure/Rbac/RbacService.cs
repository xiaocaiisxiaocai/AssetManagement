using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Rbac;

public class RbacService : IRbacService
{
    private const int MaxUserImportRows = 1000;
    private readonly AppDbContext _db;

    public RbacService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(string? keyword, int page, int pageSize)
    {
        var query = _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(x => x.EmployeeNo.Contains(kw) || x.Name.Contains(kw));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(x => x.EmployeeNo)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var departmentMap = await BuildDepartmentMapAsync(users.Select(x => x.DepartmentId));
        var items = users.Select(x => ToUserDto(x, departmentMap)).ToList();

        return new PagedResult<UserDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        EnsureSingleRole(request.RoleIds);
        var employeeNo = request.EmployeeNo.Trim();
        await EnsureEmployeeNoAvailable(employeeNo);
        var password = !string.IsNullOrWhiteSpace(request.Password)
            ? request.Password
            : AppConstants.DefaultUserPassword;

        var user = new User
        {
            EmployeeNo = employeeNo,
            Name = request.Name.Trim(),
            Email = request.Email,
            Phone = request.Phone,
            DepartmentId = request.DepartmentId,
            SupervisorId = request.SupervisorId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await RewriteUserRoles(user.Id, request.RoleIds);
        return await LoadUserDto(user.Id);
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        EnsureSingleRole(request.RoleIds);
        var user = await _db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4041, "用户不存在");
        user.Name = request.Name.Trim();
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.DepartmentId = request.DepartmentId;
        user.SupervisorId = request.SupervisorId;
        await RewriteUserRoles(id, request.RoleIds);
        await _db.SaveChangesAsync();
        return await LoadUserDto(id);
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _db.Users
            .AsTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4041, "用户不存在");

        if (user.UserRoles.Any(x => x.Role?.Code == "admin"))
        {
            var adminCount = await _db.UserRoles
                .CountAsync(x => x.Role != null && x.Role.Code == "admin");
            if (adminCount <= 1)
            {
                throw new BizException(4094, "至少保留一个系统管理员");
            }
        }
        await EnsureUserNotReferencedAsync(id);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(int id)
    {
        var user = await _db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4041, "用户不存在");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(AppConstants.DefaultUserPassword);
        await _db.SaveChangesAsync();
    }

    public async Task ToggleUserStatusAsync(int id, bool? isActive = null)
    {
        var user = await _db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4041, "用户不存在");
        user.IsActive = isActive ?? !user.IsActive;
        await _db.SaveChangesAsync();
    }

    public async Task<byte[]> BuildUserImportTemplateAsync()
    {
        var roleName = await _db.Roles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new BizException(4001, "请先创建启用状态的角色");
        }

        return XlsxTable.Write(new[]
        {
            new[] { "工号", "姓名", "邮箱", "部门名称", "角色名称" },
            new[] { "1002", "张三", "1002@example.local", "", roleName }
        });
    }

    public async Task<UserImportResultDto> ImportUsersAsync(Stream file)
    {
        var preview = await ValidateUserImportAsync(file);
        var rows = preview.Rows;
        var invalidRows = rows.Where(x => !x.IsValid).ToList();
        if (invalidRows.Count > 0)
        {
            return preview with { SuccessCount = 0 };
        }

        var roleMap = await _db.Roles
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Name, x => x.Id);
        var departmentMap = await _db.Departments
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Name, x => x.Id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var row in rows)
        {
            var user = new User
            {
                EmployeeNo = row.EmployeeNo,
                Name = row.Name,
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email,
                DepartmentId = string.IsNullOrWhiteSpace(row.DepartmentName) ? null : departmentMap[row.DepartmentName],
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AppConstants.DefaultUserPassword),
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleMap[row.RoleName]
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new UserImportResultDto
        {
            SuccessCount = rows.Count,
            FailedCount = 0,
            Rows = rows
        };
    }

    public async Task<UserImportResultDto> ValidateUserImportAsync(Stream file)
    {
        var rows = await ReadAndValidateUserImportRowsAsync(file);
        var invalidCount = rows.Count(x => !x.IsValid);
        return new UserImportResultDto
        {
            SuccessCount = rows.Count - invalidCount,
            FailedCount = invalidCount,
            Rows = rows
        };
    }

    public async Task<PagedResult<RoleDto>> GetRolesAsync(int page, int pageSize)
    {
        var query = _db.Roles
            .Include(x => x.RolePermissions)
            .Include(x => x.RoleMenus)
            .OrderBy(x => x.Id);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ToRoleDto(x)).ToListAsync();
        return new PagedResult<RoleDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<RoleDto> GetRoleAsync(int id) => await LoadRoleDto(id);

    public async Task<RoleDto> CreateRoleAsync(RoleDto request)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();
        await EnsureRoleCodeAvailable(code);
        await EnsureRoleNameAvailable(name);
        var role = new Role { Code = code, Name = name, IsActive = request.IsActive };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        await RewriteRolePermissions(role.Id, request.PermissionIds);
        await RewriteRoleMenus(role.Id, request.MenuIds);
        return await LoadRoleDto(role.Id);
    }

    public async Task<RoleDto> UpdateRoleAsync(int id, RoleDto request)
    {
        var role = await _db.Roles.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");
        var name = request.Name.Trim();
        await EnsureRoleNameAvailable(name, id);
        role.Name = name;
        role.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return await LoadRoleDto(id);
    }

    public async Task DeleteRoleAsync(int id)
    {
        var role = await _db.Roles.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");
        if (await _db.UserRoles.AnyAsync(x => x.RoleId == id))
        {
            throw new BizException(4094, "角色已被用户使用，不能删除");
        }
        if (await _db.RolePermissions.AnyAsync(x => x.RoleId == id))
        {
            throw new BizException(4094, "角色已配置权限，不能删除");
        }
        if (await _db.RoleMenus.AnyAsync(x => x.RoleId == id))
        {
            throw new BizException(4094, "角色已配置菜单，不能删除");
        }
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
    }

    public async Task<RoleDto> SetRolePermissionsAsync(int id, int[] permissionIds)
    {
        await RewriteRolePermissions(id, permissionIds);
        return await LoadRoleDto(id);
    }

    public async Task<RoleDto> SetRoleMenusAsync(int id, int[] menuIds)
    {
        await RewriteRoleMenus(id, menuIds);
        return await LoadRoleDto(id);
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
        => await _db.Permissions.OrderBy(x => x.Module).ThenBy(x => x.Code).Select(x => ToPermissionDto(x)).ToListAsync();

    public async Task<PermissionDto> CreatePermissionAsync(PermissionDto request)
    {
        var code = request.Code.Trim();
        await EnsurePermissionCodeAvailable(code);
        var permission = new Permission { Code = code, Name = request.Name.Trim(), Module = request.Module };
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();
        return ToPermissionDto(permission);
    }

    public async Task<PermissionDto> UpdatePermissionAsync(int id, PermissionDto request)
    {
        var permission = await _db.Permissions.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4043, "权限不存在");
        var code = request.Code.Trim();
        await EnsurePermissionCodeAvailable(code, id);
        permission.Code = code;
        permission.Name = request.Name.Trim();
        permission.Module = request.Module;
        await _db.SaveChangesAsync();
        return ToPermissionDto(permission);
    }

    public async Task DeletePermissionAsync(int id)
    {
        var permission = await _db.Permissions.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4043, "权限不存在");
        if (await _db.RolePermissions.AnyAsync(x => x.PermissionId == id))
        {
            throw new BizException(4094, "权限已被角色使用，不能删除");
        }
        if (await _db.Menus.AnyAsync(x => x.PermissionCode == permission.Code))
        {
            throw new BizException(4094, "权限已被菜单使用，不能删除");
        }
        _db.Permissions.Remove(permission);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MenuDto>> GetMenusAsync()
    {
        var menus = await _db.Menus.OrderBy(x => x.Sort).ThenBy(x => x.Id).ToListAsync();
        return BuildMenuTree(null, menus);
    }

    public async Task<MenuDto> CreateMenuAsync(MenuDto request)
    {
        var menu = new Menu
        {
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Title = request.Title.Trim(),
            Path = request.Path,
            Component = request.Component,
            Icon = request.Icon,
            Sort = request.Sort,
            Type = request.Type,
            PermissionCode = request.PermissionCode
        };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();
        return ToMenuDto(menu);
    }

    public async Task<MenuDto> UpdateMenuAsync(int id, MenuDto request)
    {
        var menu = await _db.Menus.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4044, "菜单不存在");
        menu.ParentId = request.ParentId;
        menu.Name = request.Name.Trim();
        menu.Title = request.Title.Trim();
        menu.Path = request.Path;
        menu.Component = request.Component;
        menu.Icon = request.Icon;
        menu.Sort = request.Sort;
        menu.Type = request.Type;
        menu.PermissionCode = request.PermissionCode;
        await _db.SaveChangesAsync();
        return ToMenuDto(menu);
    }

    public async Task DeleteMenuAsync(int id)
    {
        var menu = await _db.Menus.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4044, "菜单不存在");
        if (await _db.Menus.AnyAsync(x => x.ParentId == id))
        {
            throw new BizException(4094, "请先删除子菜单");
        }
        if (await _db.RoleMenus.AnyAsync(x => x.MenuId == id))
        {
            throw new BizException(4094, "菜单已被角色使用，不能删除");
        }
        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync();
    }

    private async Task RewriteUserRoles(int userId, IEnumerable<int> roleIds)
    {
        _db.UserRoles.RemoveRange(_db.UserRoles.Where(x => x.UserId == userId));
        var distinctIds = roleIds.Distinct().ToArray();
        _db.UserRoles.AddRange(distinctIds.Select(roleId => new UserRole { UserId = userId, RoleId = roleId }));
        await _db.SaveChangesAsync();
    }

    private async Task EnsureUserNotReferencedAsync(int id)
    {
        if (await _db.Departments.AnyAsync(x => x.ManagerId == id))
        {
            throw new BizException(4094, "用户已被部门负责人使用，不能删除");
        }
        if (await _db.Users.AnyAsync(x => x.SupervisorId == id))
        {
            throw new BizException(4094, "用户已被上级关系使用，不能删除");
        }
        if (await _db.Assets.AnyAsync(x => x.CustodianId == id))
        {
            throw new BizException(4094, "用户已被资产保管人使用，不能删除");
        }
        if (await _db.TestProjects.AnyAsync(x => x.OwnerId == id))
        {
            throw new BizException(4094, "用户已被项目负责人使用，不能删除");
        }
        if (await _db.TestMaterials.AnyAsync(x => x.CustodianId == id))
        {
            throw new BizException(4094, "用户已被测试料件保管人使用，不能删除");
        }
        if (await _db.ApprovalFlows.AnyAsync(x => x.ApplicantId == id || x.TransfereeId == id))
        {
            throw new BizException(4094, "用户已被资产流转记录使用，不能删除");
        }
        if (await _db.MaterialFlows.AnyAsync(x => x.ApplicantId == id || x.TransfereeId == id))
        {
            throw new BizException(4094, "用户已被料件流转记录使用，不能删除");
        }
        if (await _db.Notifications.AnyAsync(x => x.UserId == id))
        {
            throw new BizException(4094, "用户已被通知记录使用，不能删除");
        }
        if (await _db.AuditLogs.AnyAsync(x => x.UserId == id))
        {
            throw new BizException(4094, "用户已被审计日志使用，不能删除");
        }
    }

    private async Task<List<UserImportRowDto>> ReadAndValidateUserImportRowsAsync(Stream file)
    {
        var rawRows = XlsxTable.Read(file).Skip(1).ToList();
        if (rawRows.Count > MaxUserImportRows)
        {
            throw new BizException(4153, $"单次导入不能超过 {MaxUserImportRows} 行");
        }

        var activeRoles = await _db.Roles
            .Where(x => x.IsActive)
            .Select(x => x.Name)
            .ToListAsync();
        var roleNames = activeRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeDepartments = await _db.Departments
            .Where(x => x.IsActive)
            .Select(x => x.Name)
            .ToListAsync();
        var departmentNames = activeDepartments.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmployeeNos = await _db.Users
            .Select(x => x.EmployeeNo)
            .ToListAsync();
        var existingEmployeeNoSet = existingEmployeeNos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateInFile = rawRows
            .Select(row => Cell(row, 0))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rawRows.Select((cells, index) =>
        {
            var employeeNo = Cell(cells, 0);
            var name = Cell(cells, 1);
            var email = Cell(cells, 2);
            var hasDepartmentColumn = cells.Count >= 5;
            var departmentName = hasDepartmentColumn ? Cell(cells, 3) : "";
            var roleName = hasDepartmentColumn ? Cell(cells, 4) : Cell(cells, 3);
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(employeeNo)) errors.Add("工号必填");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("姓名必填");
            if (string.IsNullOrWhiteSpace(roleName)) errors.Add("角色名称必填");
            if (!string.IsNullOrWhiteSpace(employeeNo) && existingEmployeeNoSet.Contains(employeeNo)) errors.Add("工号已存在");
            if (!string.IsNullOrWhiteSpace(employeeNo) && duplicateInFile.Contains(employeeNo)) errors.Add("工号在导入文件中重复");
            if (!string.IsNullOrWhiteSpace(departmentName) && !departmentNames.Contains(departmentName)) errors.Add("部门名称不存在或已停用");
            if (!string.IsNullOrWhiteSpace(roleName) && !roleNames.Contains(roleName)) errors.Add("角色名称不存在或已停用");

            return new UserImportRowDto
            {
                Row = index + 2,
                EmployeeNo = employeeNo,
                Name = name,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName,
                RoleName = roleName,
                IsValid = errors.Count == 0,
                Error = string.Join("；", errors)
            };
        }).ToList();
    }

    private static string Cell(IReadOnlyList<string> cells, int index)
        => index < cells.Count ? cells[index].Trim() : "";

    private static void EnsureSingleRole(IEnumerable<int> roleIds)
    {
        if (roleIds.Distinct().Count() != 1)
        {
            throw new BizException(4001, "请选择角色");
        }
    }

    private async Task EnsureEmployeeNoAvailable(string employeeNo)
    {
        if (await _db.Users.AnyAsync(x => x.EmployeeNo == employeeNo))
        {
            throw new BizException(4094, "工号已存在");
        }
    }

    private async Task EnsureRoleCodeAvailable(string code)
    {
        if (await _db.Roles.AnyAsync(x => x.Code == code))
        {
            throw new BizException(4094, "角色编码已存在");
        }
    }

    private async Task EnsureRoleNameAvailable(string name, int? selfId = null)
    {
        if (await _db.Roles.AnyAsync(x => x.Name == name && x.Id != selfId))
        {
            throw new BizException(4094, "角色名称已存在");
        }
    }

    private async Task EnsurePermissionCodeAvailable(string code, int? selfId = null)
    {
        if (await _db.Permissions.AnyAsync(x => x.Code == code && x.Id != selfId))
        {
            throw new BizException(4094, "权限编码已存在");
        }
    }

    private async Task RewriteRolePermissions(int roleId, IEnumerable<int> permissionIds)
    {
        if (!await _db.Roles.AnyAsync(x => x.Id == roleId))
        {
            throw new BizException(4042, "角色不存在");
        }

        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x => x.RoleId == roleId));
        _db.RolePermissions.AddRange(permissionIds.Distinct().Select(id => new RolePermission
        {
            RoleId = roleId,
            PermissionId = id
        }));
        await _db.SaveChangesAsync();
    }

    private async Task RewriteRoleMenus(int roleId, IEnumerable<int> menuIds)
    {
        if (!await _db.Roles.AnyAsync(x => x.Id == roleId))
        {
            throw new BizException(4042, "角色不存在");
        }

        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x => x.RoleId == roleId));
        _db.RoleMenus.AddRange(menuIds.Distinct().Select(id => new RoleMenu
        {
            RoleId = roleId,
            MenuId = id
        }));
        await _db.SaveChangesAsync();
    }

    private async Task<UserDto> LoadUserDto(int id)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == id);
        var departmentMap = await BuildDepartmentMapAsync(new[] { user.DepartmentId });
        return ToUserDto(user, departmentMap);
    }

    private async Task<Dictionary<int, string>> BuildDepartmentMapAsync(IEnumerable<int?> ids)
    {
        var departmentIds = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (departmentIds.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _db.Departments
            .Where(x => departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    private async Task<RoleDto> LoadRoleDto(int id)
    {
        var role = await _db.Roles
            .Include(x => x.RolePermissions)
            .Include(x => x.RoleMenus)
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");
        return ToRoleDto(role);
    }

    private static UserDto ToUserDto(User x, IReadOnlyDictionary<int, string>? departments = null) => new()
    {
        Id = x.Id,
        EmployeeNo = x.EmployeeNo,
        Name = x.Name,
        Email = x.Email,
        Phone = x.Phone,
        IsActive = x.IsActive,
        DepartmentId = x.DepartmentId,
        DepartmentName = x.DepartmentId.HasValue && departments?.TryGetValue(x.DepartmentId.Value, out var departmentName) == true
            ? departmentName
            : null,
        SupervisorId = x.SupervisorId,
        RoleIds = x.UserRoles.Select(r => r.RoleId).ToArray(),
        RoleNames = x.UserRoles
            .Where(r => r.Role is not null)
            .Select(r => r.Role.Name)
            .ToArray()
    };

    private static RoleDto ToRoleDto(Role x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        IsActive = x.IsActive,
        PermissionIds = x.RolePermissions.Select(p => p.PermissionId).ToArray(),
        MenuIds = x.RoleMenus.Select(m => m.MenuId).ToArray()
    };

    private static PermissionDto ToPermissionDto(Permission x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Module = x.Module
    };

    private static MenuDto ToMenuDto(Menu x) => new()
    {
        Id = x.Id,
        ParentId = x.ParentId,
        Name = x.Name,
        Title = x.Title,
        Path = x.Path,
        Component = x.Component,
        Icon = x.Icon,
        Sort = x.Sort,
        Type = x.Type,
        PermissionCode = x.PermissionCode
    };

    private static List<MenuDto> BuildMenuTree(int? parentId, List<Menu> menus)
        => menus
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToMenuDto(x);
                return dto with { Children = BuildMenuTree(x.Id, menus) };
            })
            .ToList();
}

