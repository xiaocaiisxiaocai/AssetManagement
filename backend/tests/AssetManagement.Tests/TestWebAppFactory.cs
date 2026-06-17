using AssetManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 集成测试工厂：每个测试类（IClassFixture）一个实例，使用各自独立、保持打开的
/// SQLite 内存库，替换掉基于 appsettings 文件库的注册，彻底隔离测试、消除并发建库竞争。
/// 放在全局命名空间，便于无论测试类在哪个命名空间都能直接引用。
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestWebAppFactory()
    {
        // DataSource=:memory: 的内存库随连接生命周期存在，必须保持连接打开
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 附件存储指向独立临时目录,避免测试写入项目目录、并隔离各测试运行
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Attachment:Path"] = Path.Combine(Path.GetTempPath(), "amtest-uploads", Guid.NewGuid().ToString("N"))
            });
        });

        builder.ConfigureServices(services =>
        {
            // 移除原 AppDbContext（基于 appsettings 的共享文件库）注册
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            // 改用本工厂实例独占的内存库
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
