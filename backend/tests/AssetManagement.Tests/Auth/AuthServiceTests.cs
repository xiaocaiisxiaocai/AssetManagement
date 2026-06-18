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

    [Fact]
    public async Task ChangePassword_with_wrong_old_password_throws()
    {
        await using var fixture = await AuthFixture.Create();
        var svc = fixture.CreateService();
        var userId = fixture.GetUserId();

        var act = () => svc.ChangePasswordAsync(userId, new ChangePasswordRequest { OldPassword = "wrong", NewPassword = "newpwd123" });

        await act.Should().ThrowAsync<BizException>()
            .Where(x => x.Code == 1002);
    }

    [Fact]
    public async Task ChangePassword_ok_updates_hash()
    {
        await using var fixture = await AuthFixture.Create();
        var svc = fixture.CreateService();
        var userId = fixture.GetUserId();
        var oldHash = fixture.GetUserPasswordHash();

        await svc.ChangePasswordAsync(userId, new ChangePasswordRequest { OldPassword = "123456", NewPassword = "newpwd123" });

        var newHash = fixture.GetUserPasswordHash();
        newHash.Should().NotBe(oldHash);
        BCrypt.Net.BCrypt.Verify("newpwd123", newHash).Should().BeTrue();
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private int _userId;

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

            var fixture = new AuthFixture(connection, db);
            fixture._userId = user.Id;
            return fixture;
        }

        public int GetUserId() => _userId;
        public string GetUserPasswordHash() => Db.Users.First(x => x.Id == _userId).PasswordHash;

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
        public string Create(int userId, string employeeNo, IEnumerable<string> permissionCodes, IEnumerable<string> roles, int? departmentId = null)
            => $"token:{userId}:{employeeNo}:{string.Join(",", permissionCodes)}:{string.Join(",", roles)}:{departmentId}";
    }
}
