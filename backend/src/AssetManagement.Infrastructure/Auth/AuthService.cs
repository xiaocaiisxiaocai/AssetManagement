using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var employeeNo = request.EmployeeNo.Trim();
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new BizException(4011, "工号或密码错误");
        }

        var activeRoles = user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsActive)
            .ToList();
        var roleCodes = activeRoles
            .Select(x => x.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var permissionCodes = activeRoles
            .SelectMany(x => x.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        return new LoginResponse
        {
            Token = _jwt.Create(user.Id, user.EmployeeNo, permissionCodes, roleCodes)
        };
    }
}

