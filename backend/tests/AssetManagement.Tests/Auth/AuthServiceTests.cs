using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Auth;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Auth;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_with_wrong_password_throws_biz()
    {
        await using var fixture = await AuthFixture.Create();
        var svc = fixture.CreateService();

        var act = () => svc.LoginAsync(new LoginRequest { EmployeeNo = "1001", Password = "bad" });

        await act.Should().ThrowAsync<BizException>()
            .Where(x => x.Code == 4011);
    }

    [Fact]
    public async Task Login_ok_returns_token()
    {
        await using var fixture = await AuthFixture.Create();
        var svc = fixture.CreateService();

        var res = await svc.LoginAsync(new LoginRequest { EmployeeNo = "1001", Password = "123456" });

        res.Token.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AuthFixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        private AppDbContext Db { get; }

        public static async Task<AuthFixture> Create()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var permission = new Permission { Code = "asset:view", Name = "查看资产", Module = "asset" };
            var role = new Role { Code = "admin", Name = "系统管理员" };
            var user = new User
            {
                EmployeeNo = "1001",
                Name = "系统管理员",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                IsActive = true
            };

            db.Permissions.Add(permission);
            db.Roles.Add(role);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();

            return new AuthFixture(connection, db);
        }

        public AuthService CreateService()
        {
            var jwt = new FakeJwtTokenService();
            return new AuthService(Db, jwt);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string Create(int userId, string employeeNo, IEnumerable<string> permissionCodes, IEnumerable<string> roles)
            => $"token:{userId}:{employeeNo}:{string.Join(",", permissionCodes)}:{string.Join(",", roles)}";
    }
}
