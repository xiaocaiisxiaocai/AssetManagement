using System.Text.Json;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Persistence.Seed;

internal static class DeploymentInitialDataSeeder
{
    private sealed record InitialDataDocument
    {
        public List<InitialDepartment> Departments { get; init; } = [];
        public List<InitialUser> Users { get; init; } = [];
    }

    private sealed record InitialDepartment
    {
        public string Key { get; init; } = "";
        public string Name { get; init; } = "";
        public string OrganizationLevelCode { get; init; } = "";
        public string? ParentKey { get; init; }
        public string? ManagerEmployeeNo { get; init; }
        public bool IsActive { get; init; } = true;
    }

    private sealed record InitialUser
    {
        public string EmployeeNo { get; init; } = "";
        public string Name { get; init; } = "";
        public string PasswordHash { get; init; } = "";
        public string RoleCode { get; init; } = "employee";
        public string? DepartmentKey { get; init; }
        public string? SupervisorEmployeeNo { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public bool IsActive { get; init; } = true;
    }

    internal static void Seed(AppDbContext db, string configuredPath)
    {
        var path = ResolvePath(configuredPath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"初始化数据文件不存在: {path}");
        }

        var document = JsonSerializer.Deserialize<InitialDataDocument>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            }) ?? throw new InvalidOperationException("初始化数据文件内容为空");

        ValidateDocument(document);
        var levelIds = db.OrganizationLevels.AsTracking()
            .Where(x => x.IsActive)
            .ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var departmentsByKey = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        var existingDepartmentsByName = db.Departments.AsTracking()
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var usedDepartmentCodes = db.Departments.AsNoTracking()
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingDepartments = document.Departments.ToList();
        var departmentSequence = 1;
        while (pendingDepartments.Count > 0)
        {
            var ready = pendingDepartments
                .Where(x => string.IsNullOrWhiteSpace(x.ParentKey)
                    || departmentsByKey.ContainsKey(x.ParentKey))
                .ToList();
            if (ready.Count == 0)
            {
                throw new InvalidOperationException("初始化组织架构存在无效父级或循环引用");
            }

            foreach (var item in ready)
            {
                if (!levelIds.TryGetValue(item.OrganizationLevelCode, out var levelId))
                {
                    throw new InvalidOperationException($"初始化组织“{item.Name}”使用了无效层级 {item.OrganizationLevelCode}");
                }
                Department? parent = null;
                InitialDepartment? parentDefinition = null;
                if (!string.IsNullOrWhiteSpace(item.ParentKey))
                {
                    parent = departmentsByKey[item.ParentKey];
                    parentDefinition = document.Departments.Single(x =>
                        string.Equals(x.Key, item.ParentKey, StringComparison.OrdinalIgnoreCase));
                    if (!OrganizationHierarchyPolicy.CanContain(
                            parentDefinition.OrganizationLevelCode,
                            item.OrganizationLevelCode))
                    {
                        throw new InvalidOperationException(
                            $"初始化组织层级无效: {parentDefinition.Name}不能包含{item.Name}");
                    }
                }

                if (!existingDepartmentsByName.TryGetValue(item.Name.Trim(), out var department))
                {
                    string code;
                    do
                    {
                        code = $"INIT-{departmentSequence++:000}";
                    } while (!usedDepartmentCodes.Add(code));
                    department = new Department
                    {
                        Name = item.Name.Trim(),
                        Code = code
                    };
                    db.Departments.Add(department);
                    existingDepartmentsByName.Add(department.Name, department);
                }
                department.ParentId = parent?.Id;
                department.OrganizationLevelId = levelId;
                department.IsActive = item.IsActive;
                db.SaveChanges();
                departmentsByKey.Add(item.Key, department);
                pendingDepartments.Remove(item);
            }
        }

        var roles = db.Roles.AsTracking().ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var usersByEmployeeNo = db.Users.AsTracking()
            .ToDictionary(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        foreach (var item in document.Users)
        {
            if (!roles.TryGetValue(item.RoleCode, out var role))
            {
                throw new InvalidOperationException($"初始化人员“{item.EmployeeNo}”使用了无效角色 {item.RoleCode}");
            }
            if (!usersByEmployeeNo.TryGetValue(item.EmployeeNo, out var user))
            {
                user = new User
                {
                    EmployeeNo = item.EmployeeNo.Trim(),
                    Name = item.Name.Trim(),
                    PasswordHash = item.PasswordHash,
                    DepartmentId = ResolveDepartmentId(item.DepartmentKey, departmentsByKey),
                    Email = item.Email,
                    Phone = item.Phone,
                    IsActive = item.IsActive
                };
                db.Users.Add(user);
                db.SaveChanges();
                usersByEmployeeNo.Add(user.EmployeeNo, user);
            }
            if (!db.UserRoles.Any(x => x.UserId == user.Id))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }
        db.SaveChanges();

        foreach (var item in document.Users.Where(x => !string.IsNullOrWhiteSpace(x.SupervisorEmployeeNo)))
        {
            if (!usersByEmployeeNo.TryGetValue(item.SupervisorEmployeeNo!, out var supervisor))
            {
                throw new InvalidOperationException($"初始化人员“{item.EmployeeNo}”的直属主管不存在");
            }
            usersByEmployeeNo[item.EmployeeNo].SupervisorId = supervisor.Id;
        }
        foreach (var item in document.Departments.Where(x => !string.IsNullOrWhiteSpace(x.ManagerEmployeeNo)))
        {
            if (!usersByEmployeeNo.TryGetValue(item.ManagerEmployeeNo!, out var manager))
            {
                throw new InvalidOperationException($"初始化组织“{item.Name}”的负责人不存在");
            }
            departmentsByKey[item.Key].ManagerId = manager.Id;
        }
        db.SaveChanges();
    }

    internal static bool FileExists(string configuredPath)
        => File.Exists(ResolvePath(configuredPath));

    private static string ResolvePath(string configuredPath)
        => Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

    private static int? ResolveDepartmentId(
        string? key,
        IReadOnlyDictionary<string, Department> departmentsByKey)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        return departmentsByKey.TryGetValue(key, out var department)
            ? department.Id
            : throw new InvalidOperationException($"初始化人员引用的组织不存在: {key}");
    }

    private static void ValidateDocument(InitialDataDocument document)
    {
        if (document.Departments.Any(x => string.IsNullOrWhiteSpace(x.Key)
            || string.IsNullOrWhiteSpace(x.Name)
            || string.IsNullOrWhiteSpace(x.OrganizationLevelCode)))
        {
            throw new InvalidOperationException("初始化组织的 key、name 和 organizationLevelCode 均不能为空");
        }
        if (document.Departments.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)
            || document.Departments.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("初始化组织的 key 和 name 必须唯一");
        }
        if (document.Users.Any(x => string.IsNullOrWhiteSpace(x.EmployeeNo)
            || string.IsNullOrWhiteSpace(x.Name)
            || string.IsNullOrWhiteSpace(x.PasswordHash)))
        {
            throw new InvalidOperationException("初始化人员的 employeeNo、name 和 passwordHash 均不能为空");
        }
        if (document.Users.GroupBy(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException("初始化人员工号必须唯一");
        }
    }
}
