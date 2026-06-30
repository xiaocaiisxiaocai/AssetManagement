using System.Diagnostics;
using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AssetManagement.Infrastructure.Audit;

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        IConfiguration configuration,
        AppDbContext db,
        ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _db = db;
        _logger = logger;
    }

    public async Task<DatabaseBackupResultDto> BackupAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync();
        var connStr = _configuration.GetConnectionString("Default")
            ?? throw new BizException(500, "缺少数据库连接配置");
        var builder = new MySqlConnectionStringBuilder(connStr);
        var backupPath = settings.GetValueOrDefault("database_backup_path")
            ?? _configuration["DatabaseBackup:Path"];
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            backupPath = Path.Combine(AppContext.BaseDirectory, "Backups");
        }
        Directory.CreateDirectory(backupPath);

        var filePath = Path.Combine(backupPath, $"assetmgmt_{DateTime.Now:yyyyMMdd_HHmmss}.sql");
        var dumpExe = _configuration["DatabaseBackup:MysqldumpPath"];
        if (string.IsNullOrWhiteSpace(dumpExe)) dumpExe = "mysqldump";

        var args = BuildArguments(builder, filePath);
        var psi = new ProcessStartInfo
        {
            FileName = dumpExe,
            Arguments = args,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new BizException(500, "无法启动 mysqldump 备份进程");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("数据库备份失败: {Error}", error);
                throw new BizException(500, "数据库备份失败，请检查 mysqldump 路径、账号权限和备份目录");
            }
        }
        catch (BizException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库备份异常");
            throw new BizException(500, "数据库备份失败，请检查 mysqldump 是否可用");
        }

        CleanupOldBackups(backupPath, settings);
        var file = new FileInfo(filePath);
        return new DatabaseBackupResultDto
        {
            FilePath = file.FullName,
            CreatedAt = DateTime.Now,
            SizeBytes = file.Exists ? file.Length : 0
        };
    }

    private string BuildArguments(MySqlConnectionStringBuilder builder, string filePath)
    {
        var host = builder.Server;
        var port = builder.Port;
        var user = builder.UserID;
        var password = builder.Password;
        var database = builder.Database;
        return $"-h {Quote(host)} -P {port} -u {Quote(user)} -p{Quote(password)} --default-character-set=utf8mb4 --single-transaction --routines --events {Quote(database)} --result-file={Quote(filePath)}";
    }

    private async Task<Dictionary<string, string>> LoadSettingsAsync()
        => await _db.SystemSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith("database_backup_"))
            .ToDictionaryAsync(x => x.Key, x => x.Value);

    private void CleanupOldBackups(string backupPath, Dictionary<string, string> settings)
    {
        var retentionText = settings.GetValueOrDefault("database_backup_retention_days")
            ?? _configuration["DatabaseBackup:RetentionDays"];
        var retentionDays = int.TryParse(retentionText, out var days)
            ? Math.Max(days, 1)
            : 30;
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        foreach (var file in Directory.GetFiles(backupPath, "assetmgmt_*.sql"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime < cutoff)
            {
                info.Delete();
            }
        }
    }

    private static string Quote(string value)
        => $"\"{value.Replace("\"", "\\\"")}\"";
}
