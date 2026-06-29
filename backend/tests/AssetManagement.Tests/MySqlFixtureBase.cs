using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AssetManagement.Tests;

/// <summary>
/// 单元测试用 MySQL 数据库 fixture 基类。
/// 每个测试类实例拥有独立数据库，Dispose 时自动 DROP。
/// </summary>
public abstract class MySqlFixtureBase : IDisposable
{
    private const string BaseConnStr = "Server=localhost;Port=3306;User=root;Password=abc+123;CharSet=utf8mb4;";
    private readonly string _dbName;
    protected readonly AppDbContext _db;
    protected string ConnectionString => $"{BaseConnStr}Database={_dbName};";

    protected MySqlFixtureBase()
    {
        _dbName = $"assetmgmt_unit_{Guid.NewGuid():N}";

        // 建库（同步）
        using var conn = new MySqlConnection(BaseConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE `{_dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        cmd.ExecuteNonQuery();

        var connStr = ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connStr, ServerVersion.AutoDetect(connStr))
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    protected AppDbContext CreateNoTrackingContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        using var conn = new MySqlConnection(BaseConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{_dbName}`;";
        cmd.ExecuteNonQuery();
        GC.SuppressFinalize(this);
    }
}
