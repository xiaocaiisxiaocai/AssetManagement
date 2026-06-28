using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

/// <summary>
/// 集成测试工厂：每个测试类（IClassFixture）一个实例，使用各自独立的 MySQL 测试库
/// （assetmgmt_test_{随机后缀}），测试结束后自动 DROP，彻底隔离测试数据。
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly string _baseConnStr = "Server=localhost;Port=3306;User=root;Password=abc+123;CharSet=utf8mb4;";

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
                ["ConnectionStrings:Default"] = $"{_baseConnStr}Database={_dbName};"
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
                o.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));
        });
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
