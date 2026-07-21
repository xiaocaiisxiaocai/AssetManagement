using AssetManagement.Application.Common;
using AssetManagement.Application.Rbac;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Auth;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AssetManagement.Infrastructure.Rbac;

public class RbacService : IRbacService
{
    private const int MaxUserImportRows = 1000;
    private readonly AppDbContext _db;

    public RbacService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(
        string? keyword,
        int page,
        int pageSize,
        int? departmentId = null,
        int? roleId = null)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(x => x.EmployeeNo.Contains(kw) || x.Name.Contains(kw));
        }
        if (departmentId.HasValue)
        {
            query = query.Where(x => x.DepartmentId == departmentId.Value);
        }
        if (roleId.HasValue)
        {
            query = query.Where(x => x.UserRoles.Any(r => r.RoleId == roleId.Value));
        }

        var total = await query.CountAsync();
        var offset = Pagination.GetOffset(page, pageSize, total);
        var users = offset.HasValue
            ? await query
                // 工号是 varchar，直接字符串排序会把 2571 排在 434 前面；按长度再按文本实现数字工号自然升序。
                .OrderBy(x => x.EmployeeNo.Length)
                .ThenBy(x => x.EmployeeNo)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip(offset.Value)
                .Take(pageSize)
                .ToListAsync()
            : [];
        var departmentMap = await BuildDepartmentMapAsync(users.Select(x => x.DepartmentId));
        var supervisorMap = await BuildUserNameMapAsync(users.Select(x => x.SupervisorId));
        var items = users.Select(x => ToUserDto(x, departmentMap, supervisorMap)).ToList();

        return new PagedResult<UserDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<UserOptionDto>> GetActiveUserOptionsAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 50)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = _db.Users.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x => x.EmployeeNo.Contains(value) || x.Name.Contains(value));
        }

        var total = await query.CountAsync();
        var offset = Pagination.GetOffset(page, pageSize, total);
        var items = offset.HasValue
            ? await query
                .OrderBy(x => x.EmployeeNo.Length)
                .ThenBy(x => x.EmployeeNo)
                .ThenBy(x => x.Id)
                .Skip(offset.Value)
                .Take(pageSize)
                .Select(x => new UserOptionDto
                {
                    Id = x.Id,
                    EmployeeNo = x.EmployeeNo,
                    Name = x.Name,
                    DepartmentName = x.DepartmentId.HasValue
                        ? _db.Departments.Where(d => d.Id == x.DepartmentId.Value).Select(d => d.Name).FirstOrDefault()
                        : null
                })
                .ToListAsync()
            : [];
        return new PagedResult<UserOptionDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WorkflowDesignerOptionsDto> GetWorkflowDesignerOptionsAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 50)
    {
        var users = await GetActiveUserOptionsAsync(keyword, page, pageSize);
        var roles = await _db.Roles.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new WorkflowDesignerRoleOptionDto { Id = x.Id, Code = x.Code, Name = x.Name })
            .ToListAsync();
        var departments = await _db.Departments.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Select(x => new WorkflowDesignerDepartmentOptionDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Name = x.Name,
                OrganizationLevelCode = x.OrganizationLevelId.HasValue
                    ? _db.OrganizationLevels.Where(level => level.Id == x.OrganizationLevelId.Value)
                        .Select(level => level.Code).FirstOrDefault()
                    : null
            })
            .ToListAsync();
        var organizationLevels = await _db.OrganizationLevels.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .Select(x => new WorkflowDesignerOrganizationLevelOptionDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name
            })
            .ToListAsync();
        return new WorkflowDesignerOptionsDto
        {
            Users = users,
            Roles = roles,
            Departments = departments,
            OrganizationLevels = organizationLevels
        };
    }

    public async Task<List<UserOptionDto>> GetActiveSupervisorOptionsAsync(string? keyword = null)
    {
        var query = _db.Users.Where(x =>
            x.IsActive &&
            x.DepartmentId.HasValue &&
            _db.Departments.Any(d => d.Id == x.DepartmentId.Value && d.IsActive) &&
            x.UserRoles.Any(ur => ur.Role.Code == "supervisor" && ur.Role.IsActive));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x => x.EmployeeNo.Contains(value) || x.Name.Contains(value));
        }

        return await query
            .OrderBy(x => x.EmployeeNo.Length)
            .ThenBy(x => x.EmployeeNo)
            .Take(500)
            .Select(x => new UserOptionDto
            {
                Id = x.Id,
                EmployeeNo = x.EmployeeNo,
                Name = x.Name,
                DepartmentName = _db.Departments
                    .Where(d => d.Id == x.DepartmentId!.Value)
                    .Select(d => d.Name)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, bool canAssignRole)
    {
        EnsureRequiredText(request.EmployeeNo, 50, "工号");
        EnsureRequiredText(request.Name, 100, "姓名");
        EnsureSingleRole(request.RoleIds);
        EnsureCanAssignUserRole(canAssignRole);
        var employeeNo = request.EmployeeNo.Trim();
        await EnsureEmployeeNoAvailable(employeeNo);
        var password = !string.IsNullOrWhiteSpace(request.Password)
            ? request.Password
            : AppConstants.DefaultUserPassword;
        if (password != AppConstants.DefaultUserPassword)
        {
            PasswordPolicy.EnsureStrong(password);
        }
        await ValidateUserRelationsAsync(request.DepartmentId, request.SupervisorId);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var user = new User
        {
            EmployeeNo = employeeNo,
            Name = request.Name.Trim(),
            Email = request.Email,
            Phone = request.Phone,
            DepartmentId = request.DepartmentId,
            SupervisorId = request.SupervisorId,
            PasswordHash = PasswordHashing.Hash(password),
            IsActive = true
        };
        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "工号已存在");
        }
        await RewriteUserRoles(user.Id, request.RoleIds);
        await transaction.CommitAsync();
        return await LoadUserDto(user.Id);
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request, int currentUserId, bool canAssignRole)
    {
        EnsureRequiredText(request.Name, 100, "姓名");
        EnsureSingleRole(request.RoleIds);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockAdminUsersAsync();
        await EnsureUserRoleChangeAllowed(id, request.RoleIds, currentUserId, canAssignRole);
        // 所有停用路径先按统一顺序锁管理员集合，再锁目标用户；业务流创建在持有相关用户锁后
        // 会重新校验活跃状态，从而关闭“检查通过后又新增在途引用”的窗口。
        var user = await _db.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4041, "用户不存在");
        await ValidateUserRelationsAsync(request.DepartmentId, request.SupervisorId, id);
        user.Name = request.Name.Trim();
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.DepartmentId = request.DepartmentId;
        user.SupervisorId = request.SupervisorId;
        await RewriteUserRoles(id, request.RoleIds);
        await _db.SaveChangesAsync();
        var result = await LoadUserDto(id);
        await transaction.CommitAsync();
        return result;
    }

    public async Task DeleteUserAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockAdminUsersAsync();
        var user = await _db.Users
            .AsTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4041, "用户不存在");

        if (user.IsActive && user.UserRoles.Any(x => x.Role is { Code: "admin", IsActive: true }))
        {
            if (!await HasOtherUsableAdminAsync(id))
            {
                throw new BizException(4094, "至少保留一个系统管理员");
            }
        }
        await EnsureUserNotReferencedAsync(id);

        await _db.AuditLogs
            .Where(x => x.UserId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UserId, (int?)null));
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ResetPasswordAsync(int id)
    {
        var user = await _db.Users.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4041, "用户不存在");
        user.PasswordHash = PasswordHashing.Hash(AppConstants.DefaultUserPassword);
        user.TokenVersion++;
        await _db.SaveChangesAsync();
    }

    public async Task ToggleUserStatusAsync(int id, bool? isActive = null)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockAdminUsersAsync();
        var user = await _db.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE Id = {id} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4041, "用户不存在");
        var nextIsActive = isActive ?? !user.IsActive;
        if (user.IsActive && !nextIsActive
            && await IsActiveAdminAsync(id)
            && !await HasOtherUsableAdminAsync(id))
        {
            throw new BizException(4094, "至少保留一个启用状态的系统管理员");
        }
        if (user.IsActive && !nextIsActive)
        {
            await EnsureUserCanBeDisabledAsync(id);
        }
        if (user.IsActive != nextIsActive)
        {
            user.IsActive = nextIsActive;
            user.TokenVersion++;
        }
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
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

    public async Task<UserImportResultDto> ImportUsersAsync(Stream file, bool canAssignRole)
    {
        EnsureCanAssignUserRole(canAssignRole);
        var preview = await ValidateUserImportAsync(file);
        var rows = preview.Rows;
        var invalidRows = rows.Where(x => !x.IsValid).ToList();
        if (invalidRows.Count > 0)
        {
            return preview with { SuccessCount = 0 };
        }

        var roleMap = (await _db.Roles
            .Where(x => x.IsActive)
            .Select(x => new { x.Name, x.Id })
            .ToListAsync()).ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var departmentMap = (await _db.Departments
            .Where(x => x.IsActive)
            .Select(x => new { x.Name, x.Id })
            .ToListAsync()).ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var row in rows)
        {
            var user = new User
            {
                EmployeeNo = row.EmployeeNo,
                Name = row.Name,
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email,
                DepartmentId = string.IsNullOrWhiteSpace(row.DepartmentName) ? null : departmentMap[row.DepartmentName],
                PasswordHash = PasswordHashing.Hash(AppConstants.DefaultUserPassword),
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

    public async Task<PagedResult<RoleDto>> GetRolesAsync(string? keyword, int page, int pageSize)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = _db.Roles
            .AsSplitQuery()
            .Include(x => x.RolePermissions)
            .Include(x => x.RoleMenus)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x => x.Code.Contains(normalizedKeyword) || x.Name.Contains(normalizedKeyword));
        }
        query = query.OrderBy(x => x.Id);
        var total = await query.CountAsync();
        var offset = Pagination.GetOffset(page, pageSize, total);
        var items = offset.HasValue
            ? await query.Skip(offset.Value).Take(pageSize).Select(x => ToRoleDto(x)).ToListAsync()
            : [];
        return new PagedResult<RoleDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<RoleDto> GetRoleAsync(int id) => await LoadRoleDto(id);

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request)
    {
        EnsureRequiredText(request.Code, 50, "角色编码");
        EnsureRequiredText(request.Name, 100, "角色名称");
        var code = request.Code.Trim();
        var name = request.Name.Trim();
        await EnsureRoleCodeAvailable(code);
        await EnsureRoleNameAvailable(name);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var role = new Role { Code = code, Name = name, IsActive = request.IsActive };
        _db.Roles.Add(role);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "角色编码或名称已存在");
        }
        await transaction.CommitAsync();
        return await LoadRoleDto(role.Id);
    }

    public async Task<RoleDto> UpdateRoleAsync(int id, UpdateRoleRequest request)
    {
        EnsureRequiredText(request.Name, 100, "角色名称");
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var role = await _db.Roles.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");
        if (role.Code == "admin" && !request.IsActive)
        {
            throw new BizException(4094, "系统管理员角色不能停用");
        }
        var name = request.Name.Trim();
        await EnsureRoleNameAvailable(name, id);
        var statusChanged = role.IsActive != request.IsActive;
        role.Name = name;
        role.IsActive = request.IsActive;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            throw new BizException(4094, "角色名称已存在");
        }
        if (statusChanged)
        {
            await BumpRoleMemberTokenVersionsAsync(id);
        }
        var result = await LoadRoleDto(id);
        await transaction.CommitAsync();
        return result;
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
        var menuIds = await _db.RoleMenus
            .Where(x => x.RoleId == id)
            .Select(x => x.MenuId)
            .ToArrayAsync();
        return await SetRoleAccessAsync(id, permissionIds, menuIds);
    }

    public async Task<RoleDto> SetRoleMenusAsync(int id, int[] menuIds)
    {
        var permissionIds = await _db.RolePermissions
            .Where(x => x.RoleId == id)
            .Select(x => x.PermissionId)
            .ToArrayAsync();
        return await SetRoleAccessAsync(id, permissionIds, menuIds);
    }

    public async Task<RoleDto> SetRoleAccessAsync(int id, int[] permissionIds, int[] menuIds)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");

        var distinctPermissionIds = permissionIds.Distinct().ToArray();
        if (await _db.Permissions.CountAsync(x => distinctPermissionIds.Contains(x.Id)) != distinctPermissionIds.Length)
        {
            throw new BizException(4043, "权限不存在");
        }

        var requestedMenuIds = menuIds.Distinct().ToArray();
        var allMenus = await _db.Menus.ToListAsync();
        if (allMenus.Count(x => requestedMenuIds.Contains(x.Id)) != requestedMenuIds.Length)
        {
            throw new BizException(4044, "菜单不存在");
        }

        var expandedMenuIds = ExpandMenuIdsWithAncestors(requestedMenuIds, allMenus);
        if (role.Code == "admin")
        {
            var allPermissionIds = await _db.Permissions.Select(x => x.Id).ToListAsync();
            var allMenuIds = allMenus.Select(x => x.Id).ToHashSet();
            if (!allPermissionIds.ToHashSet().SetEquals(distinctPermissionIds)
                || !allMenuIds.SetEquals(expandedMenuIds))
            {
                throw new BizException(4094, "系统管理员角色必须保留全部权限和菜单");
            }
        }
        var selectedPermissionCodes = (await _db.Permissions
            .Where(x => distinctPermissionIds.Contains(x.Id))
            .Select(x => x.Code)
            .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);
        var missingMenuPermissions = allMenus
            .Where(x => expandedMenuIds.Contains(x.Id) && !string.IsNullOrWhiteSpace(x.PermissionCode))
            .Where(x => !selectedPermissionCodes.Contains(x.PermissionCode!))
            .Select(x => x.Title)
            .Distinct()
            .ToArray();
        if (missingMenuPermissions.Length > 0)
        {
            throw new BizException(4001, $"以下菜单缺少访问权限：{string.Join("、", missingMenuPermissions)}");
        }

        var currentPermissionIds = await _db.RolePermissions
            .Where(x => x.RoleId == id)
            .Select(x => x.PermissionId)
            .ToArrayAsync();
        var currentMenuIds = await _db.RoleMenus
            .Where(x => x.RoleId == id)
            .Select(x => x.MenuId)
            .ToArrayAsync();
        var accessChanged = !currentPermissionIds.ToHashSet().SetEquals(distinctPermissionIds)
            || !currentMenuIds.ToHashSet().SetEquals(expandedMenuIds);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x => x.RoleId == id));
        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x => x.RoleId == id));
        _db.RolePermissions.AddRange(distinctPermissionIds.Select(permissionId => new RolePermission
        {
            RoleId = id,
            PermissionId = permissionId
        }));
        _db.RoleMenus.AddRange(expandedMenuIds.Select(menuId => new RoleMenu
        {
            RoleId = id,
            MenuId = menuId
        }));
        await _db.SaveChangesAsync();
        if (accessChanged)
        {
            await BumpRoleMemberTokenVersionsAsync(id);
        }
        await transaction.CommitAsync();
        return await LoadRoleDto(id);
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
        => await _db.Permissions.OrderBy(x => x.Module).ThenBy(x => x.Code).Select(x => ToPermissionDto(x)).ToListAsync();

    public async Task<RoleAccessOptionsDto> GetRoleAccessOptionsAsync()
        => new()
        {
            Permissions = await GetPermissionsAsync(),
            Menus = await GetMenusAsync()
        };

    public async Task<PermissionDto> CreatePermissionAsync(PermissionDto request)
    {
        EnsureRequiredText(request.Code, 100, "权限编码");
        EnsureRequiredText(request.Name, 100, "权限名称");
        var code = request.Code.Trim();
        await EnsurePermissionCodeAvailable(code);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var permission = new Permission { Code = code, Name = request.Name.Trim(), Module = request.Module };
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();
        var adminRoleId = await _db.Roles.Where(x => x.Code == "admin").Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (adminRoleId.HasValue)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = adminRoleId.Value, PermissionId = permission.Id });
            await _db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return ToPermissionDto(permission);
    }

    public async Task<PermissionDto> UpdatePermissionAsync(int id, PermissionDto request)
    {
        EnsureRequiredText(request.Code, 100, "权限编码");
        EnsureRequiredText(request.Name, 100, "权限名称");
        var permission = await _db.Permissions.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4043, "权限不存在");
        var code = request.Code.Trim();
        if (!string.Equals(permission.Code, code, StringComparison.Ordinal))
        {
            throw new BizException(4094, "权限编码创建后不能修改");
        }
        permission.Name = request.Name.Trim();
        permission.Module = request.Module;
        await _db.SaveChangesAsync();
        return ToPermissionDto(permission);
    }

    public async Task DeletePermissionAsync(int id)
    {
        var permission = await _db.Permissions.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4043, "权限不存在");
        if (await _db.Menus.AnyAsync(x => x.PermissionCode == permission.Code))
        {
            throw new BizException(4094, "权限已被菜单使用，不能删除");
        }
        if (await _db.RolePermissions.AnyAsync(x => x.PermissionId == id))
        {
            throw new BizException(4094, "权限已被角色使用，不能删除");
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
        EnsureRequiredText(request.Name, 100, "菜单名称");
        EnsureRequiredText(request.Title, 100, "菜单标题");
        await ValidateMenuParentAsync(null, request.ParentId);
        var (menuType, permissionCode) = await ValidateMenuMetadataAsync(request.Type, request.PermissionCode);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var menu = new Menu
        {
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            Title = request.Title.Trim(),
            Path = request.Path,
            Component = request.Component,
            Icon = request.Icon,
            Sort = request.Sort,
            Type = menuType,
            PermissionCode = permissionCode
        };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();
        var adminRoleId = await _db.Roles.Where(x => x.Code == "admin").Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (adminRoleId.HasValue)
        {
            _db.RoleMenus.Add(new RoleMenu { RoleId = adminRoleId.Value, MenuId = menu.Id });
            await _db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return ToMenuDto(menu);
    }

    public async Task<MenuDto> UpdateMenuAsync(int id, MenuDto request)
    {
        EnsureRequiredText(request.Name, 100, "菜单名称");
        EnsureRequiredText(request.Title, 100, "菜单标题");
        var menu = await _db.Menus.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4044, "菜单不存在");
        await ValidateMenuParentAsync(id, request.ParentId);
        var (menuType, permissionCode) = await ValidateMenuMetadataAsync(request.Type, request.PermissionCode);
        menu.ParentId = request.ParentId;
        menu.Name = request.Name.Trim();
        menu.Title = request.Title.Trim();
        menu.Path = request.Path;
        menu.Component = request.Component;
        menu.Icon = request.Icon;
        menu.Sort = request.Sort;
        menu.Type = menuType;
        menu.PermissionCode = permissionCode;
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
        var distinctIds = roleIds.Distinct().ToArray();
        if (await _db.Roles.CountAsync(x => distinctIds.Contains(x.Id)) != distinctIds.Length)
        {
            throw new BizException(4042, "角色不存在");
        }
        _db.UserRoles.RemoveRange(_db.UserRoles.Where(x => x.UserId == userId));
        _db.UserRoles.AddRange(distinctIds.Select(roleId => new UserRole { UserId = userId, RoleId = roleId }));
        await _db.SaveChangesAsync();
    }

    private async Task EnsureUserRoleChangeAllowed(int userId, IEnumerable<int> requestedRoleIds, int currentUserId, bool canAssignRole)
    {
        var requested = requestedRoleIds.Distinct().OrderBy(x => x).ToArray();
        var existing = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToArrayAsync();
        if (existing.OrderBy(x => x).SequenceEqual(requested))
        {
            return;
        }

        if (userId == currentUserId)
        {
            throw new BizException(4094, "不能修改自己的角色");
        }

        EnsureCanAssignUserRole(canAssignRole);

        var removesLastUsableAdmin = await IsActiveAdminAsync(userId)
            && !await _db.Roles.AnyAsync(x => requested.Contains(x.Id) && x.Code == "admin" && x.IsActive)
            && !await HasOtherUsableAdminAsync(userId);
        if (removesLastUsableAdmin)
        {
            throw new BizException(4094, "至少保留一个启用状态的系统管理员");
        }
    }

    private async Task<bool> IsActiveAdminAsync(int userId)
        => await _db.Users.AnyAsync(x => x.Id == userId && x.IsActive
            && x.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "admin" && ur.Role.IsActive));

    private async Task<bool> HasOtherUsableAdminAsync(int excludedUserId)
        => await _db.Users.AnyAsync(x => x.Id != excludedUserId && x.IsActive
            && x.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "admin" && ur.Role.IsActive));

    private async Task ValidateUserRelationsAsync(int? departmentId, int? supervisorId, int? selfId = null)
    {
        if (departmentId.HasValue
            && !await _db.Departments.AnyAsync(x => x.Id == departmentId.Value && x.IsActive))
        {
            throw new BizException(4045, "部门不存在或已停用");
        }
        if (!supervisorId.HasValue)
        {
            return;
        }
        if (supervisorId == selfId)
        {
            throw new BizException(4001, "直属上级不能设置为用户本人");
        }
        if (!await _db.Users.AnyAsync(x => x.Id == supervisorId.Value
            && x.IsActive
            && x.DepartmentId.HasValue
            && _db.Departments.Any(d => d.Id == x.DepartmentId.Value && d.IsActive)
            && x.UserRoles.Any(ur => ur.Role.Code == "supervisor" && ur.Role.IsActive)))
        {
            throw new BizException(4041, "直属上级必须是有效部门的启用主管");
        }
    }

    private async Task LockAdminUsersAsync()
    {
        await _db.Users.FromSqlRaw("""
            SELECT u.*
            FROM users u
            WHERE EXISTS (
                SELECT 1
                FROM user_roles ur
                INNER JOIN roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = u.Id AND r.Code = 'admin'
            )
            ORDER BY u.Id
            FOR UPDATE
            """).LoadAsync();
    }

    private static void EnsureCanAssignUserRole(bool canAssignRole)
    {
        if (!canAssignRole)
        {
            throw new BizException(4031, "没有分配用户角色权限");
        }
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
        if (await _db.Assets.AnyAsync(x => x.InitialCustodianId == id))
        {
            throw new BizException(4094, "用户已被资产初始保管记录使用，不能删除");
        }
        if (await _db.TestProjects.AnyAsync(x => x.OwnerId == id))
        {
            throw new BizException(4094, "用户已被项目负责人使用，不能删除");
        }
        if (await _db.TestProjectFollowups.AnyAsync(x => x.FilledById == id))
        {
            throw new BizException(4094, "用户已被项目跟进记录使用，不能删除");
        }
        if (await _db.TestMaterials.AnyAsync(x => x.CustodianId == id))
        {
            throw new BizException(4094, "用户已被测试料件保管人使用，不能删除");
        }
        if (await _db.ApprovalFlows.AnyAsync(x =>
                x.ApplicantId == id || x.TransfereeId == id || x.SourceCustodianId == id))
        {
            throw new BizException(4094, "用户已被资产流转记录使用，不能删除");
        }
        if (await _db.FlowRecords.AnyAsync(x => x.OperatorUserId == id) ||
            await _db.MaterialFlowRecords.AnyAsync(x => x.OperatorUserId == id) ||
            await _db.TestMaterialRecords.AnyAsync(x => x.OperatorUserId == id))
        {
            throw new BizException(4094, "用户已被审批或料件操作记录使用，不能删除");
        }
        if (await _db.MaterialFlows.AnyAsync(x => x.ApplicantId == id || x.TransfereeId == id))
        {
            throw new BizException(4094, "用户已被料件流转记录使用，不能删除");
        }
        if (await _db.Notifications.AnyAsync(x => x.UserId == id))
        {
            throw new BizException(4094, "用户已被通知记录使用，不能删除");
        }
    }

    private async Task EnsureUserCanBeDisabledAsync(int userId)
    {
        if (await _db.Assets.AsNoTracking()
                .AnyAsync(x => !x.IsDeleted && x.Status == AssetStatus.Borrowed && x.CustodianId == userId))
        {
            throw new BizException(4092, "该用户仍保管借出中的资产，归还或转交后才能停用");
        }

        if (await _db.TestMaterials.AsNoTracking()
                .AnyAsync(x => !x.IsDeleted && x.Status == MaterialStatus.InUse && x.CustodianId == userId))
        {
            throw new BizException(4092, "该用户仍保管在用料件，退回或转交后才能停用");
        }

        if (await _db.ApprovalFlows.AsNoTracking()
                .AnyAsync(x => x.Status == "pending" && x.BizType == "borrow" && x.ApplicantId == userId))
        {
            throw new BizException(4092, "该用户有进行中的资产借用申请，流程结束前不能停用");
        }

        if (await _db.ApprovalFlows.AsNoTracking()
                .AnyAsync(x => x.Status == "pending" && x.TransfereeId == userId))
        {
            throw new BizException(4092, "该用户是在途资产流转的受让人，流程结束前不能停用");
        }

        if (await _db.MaterialFlows.AsNoTracking()
                .AnyAsync(x => x.Status == "pending" && x.TransfereeId == userId))
        {
            throw new BizException(4092, "该用户是在途料件流转的受让人，流程结束前不能停用");
        }

        var approvalFlows = await _db.ApprovalFlows.AsNoTracking()
            .Where(x => x.Status == "pending")
            .ToListAsync();
        if (await HasPendingApprovalAssignmentAsync(approvalFlows, userId))
        {
            throw new BizException(4092, "该用户仍有未签资产审批任务，处理或转交后才能停用");
        }

        var materialFlows = await _db.MaterialFlows.AsNoTracking()
            .Where(x => x.Status == "pending")
            .ToListAsync();
        if (await HasPendingApprovalAssignmentAsync(materialFlows, userId))
        {
            throw new BizException(4092, "该用户仍有未签料件审批任务，处理或转交后才能停用");
        }
    }

    private async Task<bool> HasPendingApprovalAssignmentAsync<TFlow>(
        IReadOnlyCollection<TFlow> flows,
        int userId)
        where TFlow : class, Domain.Workflow.IBpmnFlowInstance
    {
        foreach (var flow in flows)
        {
            foreach (var nodeId in flow.CurrentNodeIds)
            {
                if (!flow.BpmnTokens.TryGetValue(nodeId, out var token)
                    || token.Status != Domain.Workflow.BpmnTokenStatus.Active)
                {
                    continue;
                }

                if (token.SignStates?.TryGetValue(userId.ToString(), out var signed) == true && !signed)
                {
                    return true;
                }
            }

            var workflowId = flow switch
            {
                ApprovalFlow assetFlow => assetFlow.WorkflowId,
                MaterialFlow materialFlow => materialFlow.WorkflowId ?? 0,
                _ => 0,
            };
            if (workflowId <= 0)
            {
                continue;
            }

            var bpmnXml = await _db.Workflows.AsNoTracking()
                .Where(x => x.Id == workflowId)
                .Select(x => x.BpmnXml)
                .SingleOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(bpmnXml))
            {
                continue;
            }

            var process = BpmnParser.Parse(bpmnXml);
            foreach (var nodeId in flow.CurrentNodeIds)
            {
                var node = process.FindNode(nodeId);
                var assignee = node?.Properties.GetValueOrDefault("assignee");
                if (!string.IsNullOrWhiteSpace(assignee)
                    && !OrganizationApprovalResolver.IsOrganizationAssignee(assignee)
                    && assignee is not ("deptManager" or "supervisor"))
                {
                    var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, assignee);
                    if (resolution.Status == ApproverIdentityResolutionStatus.Unique
                        && resolution.UserIds[0] == userId)
                    {
                        return true;
                    }
                }

                var candidateUsers = node?.Properties.GetValueOrDefault("candidateUsers");
                if (string.IsNullOrWhiteSpace(candidateUsers))
                {
                    continue;
                }
                var resolvedCandidates = new HashSet<int>();
                foreach (var identity in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var candidateResolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, identity);
                    if (candidateResolution.Status == ApproverIdentityResolutionStatus.Unique)
                    {
                        resolvedCandidates.Add(candidateResolution.UserIds[0]);
                    }
                }
                if (resolvedCandidates.SetEquals(new[] { userId }))
                {
                    return true;
                }
            }
        }

        return false;
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
            if (employeeNo.Length > 50) errors.Add("工号不能超过 50 个字符");
            if (name.Length > 100) errors.Add("姓名不能超过 100 个字符");
            if (email.Length > 200) errors.Add("邮箱不能超过 200 个字符");
            if (!string.IsNullOrWhiteSpace(email)
                && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                errors.Add("邮箱格式不正确");
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

    private static void EnsureRequiredText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BizException(4001, $"{fieldName}必填");
        }
        if (value.Trim().Length > maxLength)
        {
            throw new BizException(4001, $"{fieldName}不能超过 {maxLength} 个字符");
        }
    }

    private async Task<(string Type, string? PermissionCode)> ValidateMenuMetadataAsync(
        string? type,
        string? permissionCode)
    {
        var normalizedType = type?.Trim().ToLowerInvariant();
        if (normalizedType is not ("menu" or "button"))
        {
            throw new BizException(4001, "菜单类型只能是 menu 或 button");
        }

        var normalizedPermissionCode = string.IsNullOrWhiteSpace(permissionCode)
            ? null
            : permissionCode.Trim();
        if (normalizedPermissionCode is not null
            && !await _db.Permissions.AnyAsync(x => x.Code == normalizedPermissionCode))
        {
            throw new BizException(4043, "菜单关联的权限编码不存在");
        }

        return (normalizedType, normalizedPermissionCode);
    }

    private async Task BumpRoleMemberTokenVersionsAsync(int roleId)
    {
        await _db.Users
            .Where(user => user.UserRoles.Any(userRole => userRole.RoleId == roleId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.TokenVersion, user => user.TokenVersion + 1));
    }

    private async Task EnsureEmployeeNoAvailable(string employeeNo)
    {
        if (await _db.Users.AnyAsync(x => x.EmployeeNo == employeeNo))
        {
            throw new BizException(4094, "工号已存在");
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is MySqlException { Number: 1062 };

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

        var distinctIds = permissionIds.Distinct().ToArray();
        if (await _db.Permissions.CountAsync(x => distinctIds.Contains(x.Id)) != distinctIds.Length)
        {
            throw new BizException(4043, "权限不存在");
        }
        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x => x.RoleId == roleId));
        _db.RolePermissions.AddRange(distinctIds.Select(id => new RolePermission
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

        var distinctIds = menuIds.Distinct().ToArray();
        if (await _db.Menus.CountAsync(x => distinctIds.Contains(x.Id)) != distinctIds.Length)
        {
            throw new BizException(4044, "菜单不存在");
        }
        _db.RoleMenus.RemoveRange(_db.RoleMenus.Where(x => x.RoleId == roleId));
        _db.RoleMenus.AddRange(distinctIds.Select(id => new RoleMenu
        {
            RoleId = roleId,
            MenuId = id
        }));
        await _db.SaveChangesAsync();
    }

    private static HashSet<int> ExpandMenuIdsWithAncestors(IEnumerable<int> menuIds, IReadOnlyCollection<Menu> allMenus)
    {
        var menuMap = allMenus.ToDictionary(x => x.Id);
        var expanded = menuIds.ToHashSet();
        foreach (var menuId in expanded.ToArray())
        {
            var cursor = menuMap[menuId];
            var visited = new HashSet<int> { cursor.Id };
            while (cursor.ParentId.HasValue)
            {
                if (!visited.Add(cursor.ParentId.Value))
                {
                    throw new BizException(4094, "菜单层级存在循环引用，请先修复菜单结构");
                }
                if (!menuMap.TryGetValue(cursor.ParentId.Value, out var parent))
                {
                    throw new BizException(4094, "菜单层级存在无效父级，请先修复菜单结构");
                }
                expanded.Add(cursor.ParentId.Value);
                cursor = parent;
            }
        }
        return expanded;
    }

    private async Task ValidateMenuParentAsync(int? menuId, int? parentId)
    {
        if (!parentId.HasValue)
        {
            return;
        }
        if (parentId == menuId)
        {
            throw new BizException(4001, "上级菜单不能设置为自身");
        }

        var menus = await _db.Menus
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync();
        var menuMap = menus.ToDictionary(x => x.Id);
        if (!menuMap.TryGetValue(parentId.Value, out var cursor))
        {
            throw new BizException(4044, "上级菜单不存在");
        }

        var visited = new HashSet<int>();
        while (true)
        {
            if (!visited.Add(cursor.Id))
            {
                throw new BizException(4094, "菜单层级存在循环引用");
            }
            if (cursor.Id == menuId)
            {
                throw new BizException(4001, "不能将菜单移动到自己的子菜单下");
            }
            if (!cursor.ParentId.HasValue)
            {
                break;
            }
            if (!menuMap.TryGetValue(cursor.ParentId.Value, out cursor))
            {
                throw new BizException(4094, "菜单层级存在无效父级");
            }
        }
    }

    private async Task<UserDto> LoadUserDto(int id)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == id);
        var departmentMap = await BuildDepartmentMapAsync(new[] { user.DepartmentId });
        var supervisorMap = await BuildUserNameMapAsync(new[] { user.SupervisorId });
        return ToUserDto(user, departmentMap, supervisorMap);
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

    private async Task<Dictionary<int, string>> BuildUserNameMapAsync(IEnumerable<int?> ids)
    {
        var userIds = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (userIds.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _db.Users
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    private async Task<RoleDto> LoadRoleDto(int id)
    {
        var role = await _db.Roles
            .AsSplitQuery()
            .Include(x => x.RolePermissions)
            .Include(x => x.RoleMenus)
            .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4042, "角色不存在");
        return ToRoleDto(role);
    }

    private static UserDto ToUserDto(
        User x,
        IReadOnlyDictionary<int, string>? departments = null,
        IReadOnlyDictionary<int, string>? supervisors = null) => new()
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
        SupervisorName = x.SupervisorId.HasValue && supervisors?.TryGetValue(x.SupervisorId.Value, out var supervisorName) == true
            ? supervisorName
            : null,
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

    private static List<MenuDto> BuildMenuTree(
        int? parentId,
        List<Menu> menus,
        IReadOnlySet<int>? ancestors = null)
    {
        ancestors ??= new HashSet<int>();
        return menus
            .Where(x => x.ParentId == parentId)
            .Where(x => !ancestors.Contains(x.Id))
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var dto = ToMenuDto(x);
                var nextAncestors = ancestors.ToHashSet();
                nextAncestors.Add(x.Id);
                return dto with { Children = BuildMenuTree(x.Id, menus, nextAncestors) };
            })
            .ToList();
    }
}

