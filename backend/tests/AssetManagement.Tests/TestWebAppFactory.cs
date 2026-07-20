using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Persistence.Seed;
using AssetManagement.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using System.Data.Common;

/// <summary>
/// 集成测试工厂：每个测试类（IClassFixture）一个实例，使用各自独立的 MySQL 测试库
/// （assetmgmt_test_{随机后缀}），测试结束后自动 DROP，彻底隔离测试数据。
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly string _baseConnStr = BuildBaseConnectionString();
    public DbCommandCounterInterceptor CommandCounter { get; } = new();

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

    public TestWebAppFactory()
    {
        _dbName = $"assetmgmt_test_{Guid.NewGuid():N}";
        // 建库
        using var conn = new MySqlConnection(_baseConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE `{_dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        cmd.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Attachment:Path"] = Path.Combine(Path.GetTempPath(), "amtest-uploads", Guid.NewGuid().ToString("N")),
                ["ConnectionStrings:Default"] = $"{_baseConnStr}Database={_dbName};",
                ["Jwt:Key"] = "asset-management-test-only-secret-key-2026",
                ["Database:AutoMigrate"] = "true",
                ["Database:AutoSeed"] = "true",
                ["ASSET_ADMIN_PASSWORD"] = "123456",
                // TestServer 的所有请求共享同一回环 IP；登录限流由独立测试显式开启验证。
                ["Security:LoginRateLimitEnabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            var connStr = $"{_baseConnStr}Database={_dbName};";
            services.AddDbContext<AppDbContext>(o =>
                o.UseMySql(connStr, ServerVersion.AutoDetect(connStr))
                    .AddInterceptors(CommandCounter)
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Users.AsTracking().Single(x => x.EmployeeNo == "1001");
        if (!admin.SupervisorId.HasValue)
        {
            var department = new Department
            {
                Name = "测试审批部门",
                Code = $"TEST-{Guid.NewGuid():N}"[..20],
                IsActive = true
            };
            db.Departments.Add(department);
            db.SaveChanges();
            var supervisor = new User
            {
                EmployeeNo = "TEST-SUPERVISOR",
                Name = "测试直属主管",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                DepartmentId = department.Id,
                IsActive = true
            };
            db.Users.Add(supervisor);
            db.SaveChanges();
            var supervisorRole = db.Roles.Single(x => x.Code == "supervisor");
            db.UserRoles.Add(new UserRole
            {
                UserId = supervisor.Id,
                RoleId = supervisorRole.Id
            });
            department.ManagerId = supervisor.Id;
            admin.SupervisorId = supervisor.Id;
            db.SaveChanges();
        }
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        // 清理测试库
        using var conn = new MySqlConnection(_baseConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{_dbName}`;";
        cmd.ExecuteNonQuery();
    }
}

public sealed class DbCommandCounterInterceptor : DbCommandInterceptor
{
    private int _readerCount;
    public int ReaderCount => Volatile.Read(ref _readerCount);
    public void Reset() => Interlocked.Exchange(ref _readerCount, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _readerCount);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
