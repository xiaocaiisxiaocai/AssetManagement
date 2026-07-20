using AssetManagement.Application.Auth;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Auth;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;
using System.Data.Common;

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
    public async Task Login_success_upgrades_legacy_standard_bcrypt_hash()
    {
        await using var fixture = await AuthFixture.Create();
        PasswordHashing.NeedsUpgrade(fixture.ReloadUserPasswordHash()).Should().BeTrue();

        await fixture.CreateService().LoginAsync(
            new LoginRequest { EmployeeNo = "1001", Password = "123456" });

        var upgraded = fixture.ReloadUserPasswordHash();
        PasswordHashing.NeedsUpgrade(upgraded).Should().BeFalse();
        PasswordHashing.Verify("123456", upgraded).Should().BeTrue();
    }

    [Fact]
    public void Enhanced_password_hash_distinguishes_long_password_suffixes()
    {
        var sharedPrefix = new string('a', 80);
        var storedHash = PasswordHashing.Hash(sharedPrefix + "1");

        PasswordHashing.Verify(sharedPrefix + "1", storedHash).Should().BeTrue();
        PasswordHashing.Verify(sharedPrefix + "2", storedHash).Should().BeFalse();
    }

    [Fact]
    public async Task Login_disabled_account_is_indistinguishable_from_unknown_account()
    {
        await using var fixture = await AuthFixture.Create();
        fixture.SetUserActive(false);
        var service = fixture.CreateService();

        var disabled = await Assert.ThrowsAsync<BizException>(() => service.LoginAsync(
            new LoginRequest { EmployeeNo = "1001", Password = "bad" }));
        var unknown = await Assert.ThrowsAsync<BizException>(() => service.LoginAsync(
            new LoginRequest { EmployeeNo = "missing-user", Password = "bad" }));

        disabled.Code.Should().Be(4011);
        disabled.Message.Should().Be(unknown.Message);
    }

    [Fact]
    public void Login_failure_counter_increment_is_atomic_under_parallel_writers()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        };

        Parallel.For(0, 100, _ => AuthService.IncrementFailureCount(cache, "same-key", options));

        cache.Get<int>("same-key").Should().Be(100);
    }

    [Fact]
    public async Task Login_wrong_password_does_not_load_role_or_permission_collections()
    {
        var recorder = new CommandRecorder();
        await using var fixture = await AuthFixture.Create(recorder);
        var service = fixture.CreateService();
        recorder.Clear();

        await Assert.ThrowsAsync<BizException>(() => service.LoginAsync(
            new LoginRequest { EmployeeNo = "1001", Password = "bad" }));

        recorder.Commands.Should().ContainSingle("credential failure must execute only the minimal credential query");
        recorder.Commands.Should().NotContain(command =>
            command.Contains("user_roles", StringComparison.OrdinalIgnoreCase)
            || command.Contains("role_permissions", StringComparison.OrdinalIgnoreCase));

        recorder.Clear();
        await service.LoginAsync(new LoginRequest { EmployeeNo = "1001", Password = "123456" });
        recorder.Commands.Should().Contain(command =>
            command.Contains("user_roles", StringComparison.OrdinalIgnoreCase),
            "authorization collections are loaded only after the credential succeeds");
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
        PasswordHashing.Verify("newpwd123", newHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_to_default_password_throws()
    {
        await using var fixture = await AuthFixture.Create();
        var svc = fixture.CreateService();
        var userId = fixture.GetUserId();

        var act = () => svc.ChangePasswordAsync(userId, new ChangePasswordRequest { OldPassword = "123456", NewPassword = "123456" });

        await act.Should().ThrowAsync<BizException>()
            .Where(x => x.Code == 1003);
    }

    [Theory]
    [InlineData("abcdef")]
    [InlineData("654321")]
    [InlineData("!!!!!!")]
    public async Task ChangePassword_allows_six_character_password_without_composition_requirement(string newPassword)
    {
        await using var fixture = await AuthFixture.Create();

        await fixture.CreateService().ChangePasswordAsync(
            fixture.GetUserId(),
            new ChangePasswordRequest { OldPassword = "123456", NewPassword = newPassword });

        PasswordHashing.Verify(newPassword, fixture.GetUserPasswordHash()).Should().BeTrue();
    }

    [Theory]
    [InlineData("12345")]
    public async Task ChangePassword_with_too_short_password_throws(string newPassword)
    {
        await using var fixture = await AuthFixture.Create();
        var act = () => fixture.CreateService().ChangePasswordAsync(
            fixture.GetUserId(),
            new ChangePasswordRequest { OldPassword = "123456", NewPassword = newPassword });

        await act.Should().ThrowAsync<BizException>().Where(x => x.Code == 1004);
    }

    [Fact]
    public async Task ChangePassword_with_more_than_12_characters_throws()
    {
        await using var fixture = await AuthFixture.Create();
        var act = () => fixture.CreateService().ChangePasswordAsync(
            fixture.GetUserId(),
            new ChangePasswordRequest { OldPassword = "123456", NewPassword = new string('a', 13) });

        await act.Should().ThrowAsync<BizException>().Where(x => x.Code == 1004);
    }

    [Fact]
    public async Task Login_department_admin_without_department_fails_closed()
    {
        await using var fixture = await AuthFixture.Create();
        fixture.SetRoleCode("supervisor");

        var act = () => fixture.CreateService().LoginAsync(
            new LoginRequest { EmployeeNo = "1001", Password = "123456" });

        await act.Should().ThrowAsync<BizException>().Where(x => x.Code == 4013);
    }

    [Fact]
    public async Task Routes_only_exposes_button_permissions_owned_by_current_user()
    {
        await using var fixture = await AuthFixture.Create();
        fixture.AddRouteWithOwnedAndUnownedButtons();

        var routes = await fixture.CreateService().GetRoutesAsync(fixture.GetUserId());

        routes.Should().ContainSingle();
        routes[0].Meta.Permissions.Should().Equal("asset:view");
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        private readonly List<string> _commands = new();
        public IReadOnlyList<string> Commands
        {
            get
            {
                lock (_commands) return _commands.ToArray();
            }
        }

        public void Clear()
        {
            lock (_commands) _commands.Clear();
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Record(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return ValueTask.FromResult(result);
        }

        private void Record(DbCommand command)
        {
            lock (_commands) _commands.Add(command.CommandText);
        }
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private static readonly string BaseConnStr = BuildBaseConnectionString();
        private readonly string _dbName;
        private int _userId;

        private static string BuildBaseConnectionString()
        {
            var password = Environment.GetEnvironmentVariable("ASSETMGMT_TEST_MYSQL_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("请先设置 ASSETMGMT_TEST_MYSQL_PASSWORD 环境变量");
            return new MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Port = 3306,
                UserID = "root",
                Password = password,
                CharacterSet = "utf8mb4",
                SslMode = MySqlSslMode.None
            }.ConnectionString + ";";
        }

        private AuthFixture(string dbName, AppDbContext db)
        {
            _dbName = dbName;
            Db = db;
        }

        private AppDbContext Db { get; }

        public static async Task<AuthFixture> Create(CommandRecorder? recorder = null)
        {
            var dbName = $"assetmgmt_auth_{Guid.NewGuid():N}";

            // 建库
            await using (var conn = new MySqlConnection(BaseConnStr))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await cmd.ExecuteNonQueryAsync();
            }

            var connStr = $"{BaseConnStr}Database={dbName};";
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(connStr, ServerVersion.AutoDetect(connStr))
                .ConfigureWarnings(warnings => warnings.Throw(
                    RelationalEventId.MultipleCollectionIncludeWarning));
            if (recorder is not null) optionsBuilder.AddInterceptors(recorder);
            var options = optionsBuilder.Options;
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

            var fixture = new AuthFixture(dbName, db);
            fixture._userId = user.Id;
            return fixture;
        }

        public int GetUserId() => _userId;
        public string GetUserPasswordHash() => Db.Users.First(x => x.Id == _userId).PasswordHash;
        public string ReloadUserPasswordHash()
        {
            Db.ChangeTracker.Clear();
            return Db.Users.First(x => x.Id == _userId).PasswordHash;
        }
        public void SetRoleCode(string code)
        {
            var role = Db.Roles.AsTracking().Single();
            role.Code = code;
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        public void SetUserActive(bool isActive)
        {
            var user = Db.Users.AsTracking().Single(x => x.Id == _userId);
            user.IsActive = isActive;
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        public void AddRouteWithOwnedAndUnownedButtons()
        {
            var role = Db.Roles.Single();
            var route = new Menu
            {
                Name = "AssetList",
                Title = "资产列表",
                Path = "/asset/list",
                Component = "/asset/list/index",
                Type = "menu"
            };
            Db.Menus.Add(route);
            Db.SaveChanges();
            Db.Menus.AddRange(
                new Menu { ParentId = route.Id, Name = "ViewButton", Title = "查看", Type = "button", PermissionCode = "asset:view" },
                new Menu { ParentId = route.Id, Name = "EditButton", Title = "编辑", Type = "button", PermissionCode = "asset:edit" });
            Db.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuId = route.Id });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        public AuthService CreateService()
        {
            var jwt = new FakeJwtTokenService();
            var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var httpContextAccessor = new FakeHttpContextAccessor();
            return new AuthService(Db, jwt, cache, httpContextAccessor);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            // 清理测试库
            await using var conn = new MySqlConnection(BaseConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{_dbName}`;";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string Create(
            int userId,
            string employeeNo,
            IEnumerable<string> permissionCodes,
            IEnumerable<string> roles,
            int? departmentId = null,
            int tokenVersion = 0)
            => $"token:{userId}:{employeeNo}:{string.Join(",", permissionCodes)}:{string.Join(",", roles)}:{departmentId}:{tokenVersion}";
    }

    private sealed class FakeHttpContextAccessor : Microsoft.AspNetCore.Http.IHttpContextAccessor
    {
        public Microsoft.AspNetCore.Http.HttpContext? HttpContext { get; set; }

        public FakeHttpContextAccessor()
        {
            var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            HttpContext = context;
        }
    }
}
