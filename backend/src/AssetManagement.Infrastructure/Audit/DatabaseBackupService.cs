using System.Diagnostics;
using AssetManagement.Application.Audit;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace AssetManagement.Infrastructure.Audit;

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        IConfiguration configuration,
        AppDbContext db,
        IHostEnvironment environment,
        ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DatabaseBackupResultDto> BackupAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync();
        var connStr = _configuration.GetConnectionString("Default")
            ?? throw new BizException(500, "缺少数据库连接配置");
        var builder = new MySqlConnectionStringBuilder(connStr);
        var backupPath = ResolveBackupPath(settings);
        Directory.CreateDirectory(backupPath);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filePath = Path.Combine(backupPath, $"assetmgmt_{timestamp}.sql");
        var packagePath = Path.Combine(backupPath, $"assetmgmt_{timestamp}.zip");
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

        DatabaseBackupPackageBuilder.Build(filePath, ResolveAttachmentPath(), packagePath);
        CleanupOldBackups(backupPath, settings);
        var file = new FileInfo(packagePath);
        return new DatabaseBackupResultDto
        {
            FilePath = file.FullName,
            CreatedAt = DateTime.Now,
            SizeBytes = file.Exists ? file.Length : 0
        };
    }

    public async Task<List<DatabaseBackupFileDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync();
        var backupPath = ResolveBackupPath(settings);
        if (!Directory.Exists(backupPath))
        {
            return new List<DatabaseBackupFileDto>();
        }

        return Directory
            .EnumerateFiles(backupPath, "assetmgmt_*.*")
            .Where(path => IsBackupFileName(Path.GetFileName(path)))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .Select(file => new DatabaseBackupFileDto
            {
                FileName = file.Name,
                FilePath = file.FullName,
                FileType = file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ? "package" : "sql",
                CreatedAt = file.LastWriteTime,
                SizeBytes = file.Length
            })
            .ToList();
    }

    public async Task<DatabaseBackupDownloadDto?> OpenAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!IsBackupFileName(fileName))
        {
            return null;
        }

        var settings = await LoadSettingsAsync();
        var backupPath = ResolveBackupPath(settings);
        var fullPath = Path.GetFullPath(Path.Combine(backupPath, fileName));
        var rootPath = Path.GetFullPath(backupPath);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return null;
        }

        var contentType = Path.GetExtension(fileName).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? "application/zip"
            : "application/sql";
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new DatabaseBackupDownloadDto(stream, fileName, contentType);
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

    private string ResolveBackupPath(Dictionary<string, string> settings)
    {
        var backupPath = settings.GetValueOrDefault("database_backup_path")
            ?? _configuration["DatabaseBackup:Path"];
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            backupPath = "Backups";
        }
        return Path.IsPathRooted(backupPath)
            ? backupPath
            : Path.Combine(_environment.ContentRootPath, backupPath);
    }

    private string ResolveAttachmentPath()
    {
        var configuredPath = _configuration["Attachment:Path"] ?? "App_Data/uploads";
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    private void CleanupOldBackups(string backupPath, Dictionary<string, string> settings)
    {
        var retentionText = settings.GetValueOrDefault("database_backup_retention_days")
            ?? _configuration["DatabaseBackup:RetentionDays"];
        var retentionDays = int.TryParse(retentionText, out var days)
            ? Math.Max(days, 1)
            : 30;
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        foreach (var file in Directory.GetFiles(backupPath, "assetmgmt_*.*").Where(path => IsBackupFileName(Path.GetFileName(path))))
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

    private static bool IsBackupFileName(string fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && !fileName.Contains('/')
           && !fileName.Contains('\\')
           && !fileName.Contains("..")
           && fileName.StartsWith("assetmgmt_", StringComparison.OrdinalIgnoreCase)
           && (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));
}
