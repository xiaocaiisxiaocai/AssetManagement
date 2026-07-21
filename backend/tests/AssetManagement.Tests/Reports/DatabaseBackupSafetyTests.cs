using System.Diagnostics;
using AssetManagement.Infrastructure.Audit;
using FluentAssertions;

namespace AssetManagement.Tests.Reports;

public class DatabaseBackupSafetyTests
{
    [Fact]
    public void Backup_and_attachment_paths_must_not_overlap()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "assetmgmt-path-test"));

        DatabaseBackupService.PathsOverlap(root, root).Should().BeTrue();
        DatabaseBackupService.PathsOverlap(Path.Combine(root, "backups"), root).Should().BeTrue();
        DatabaseBackupService.PathsOverlap(Path.Combine(root, "backups"), Path.Combine(root, "uploads"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Cancelling_backup_wait_terminates_child_process()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            ArgumentList = { "-NoProfile", "-Command", "Start-Sleep -Seconds 30" },
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        process.Should().NotBeNull();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var action = () => DatabaseBackupService.WaitForProcessAsync(process!, cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        process!.HasExited.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelling_package_removes_current_sql_zip_and_temp_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-backup-cancel", Guid.NewGuid().ToString("N"));
        var attachments = Path.Combine(root, "uploads");
        var sqlFile = Path.Combine(root, "assetmgmt_current.sql");
        var packageFile = Path.Combine(root, "assetmgmt_current.zip");
        var staleTempFile = $"{packageFile}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(attachments);
        await File.WriteAllTextAsync(sqlFile, "select 1;");
        await File.WriteAllTextAsync(staleTempFile, "partial package");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            var action = () => DatabaseBackupService.BuildPackageSafelyAsync(
                sqlFile,
                attachments,
                packageFile,
                cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
            File.Exists(sqlFile).Should().BeFalse();
            File.Exists(packageFile).Should().BeFalse();
            Directory.EnumerateFiles(root, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Successful_package_removes_plaintext_sql_and_keeps_zip()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-backup-success", Guid.NewGuid().ToString("N"));
        var attachments = Path.Combine(root, "uploads");
        var sqlFile = Path.Combine(root, "assetmgmt_current.sql");
        var packageFile = Path.Combine(root, "assetmgmt_current.zip");
        var legacySqlFile = Path.Combine(root, "assetmgmt_legacy.sql");
        var legacyPackageFile = Path.Combine(root, "assetmgmt_legacy.zip");
        var standaloneSqlFile = Path.Combine(root, "assetmgmt_standalone.sql");
        Directory.CreateDirectory(attachments);
        await File.WriteAllTextAsync(sqlFile, "select 1;");
        await File.WriteAllTextAsync(legacySqlFile, "select legacy;");
        await File.WriteAllTextAsync(legacyPackageFile, "legacy package");
        await File.WriteAllTextAsync(standaloneSqlFile, "select standalone;");

        try
        {
            await DatabaseBackupService.BuildPackageSafelyAsync(sqlFile, attachments, packageFile);

            File.Exists(sqlFile).Should().BeFalse();
            File.Exists(packageFile).Should().BeTrue();
            File.Exists(legacySqlFile).Should().BeFalse("已有 ZIP 的历史明文 SQL 不应继续重复保留");
            File.Exists(legacyPackageFile).Should().BeTrue();
            File.Exists(standaloneSqlFile).Should().BeTrue("没有 ZIP 副本的旧 SQL 仍是有效备份");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Current_sql_cleanup_failure_fails_backup_and_removes_generated_package()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-backup-current-lock", Guid.NewGuid().ToString("N"));
        var attachments = Path.Combine(root, "uploads");
        var sqlFile = Path.Combine(root, "assetmgmt_current.sql");
        var packageFile = Path.Combine(root, "assetmgmt_current.zip");
        Directory.CreateDirectory(attachments);
        await File.WriteAllTextAsync(sqlFile, "select 1;");

        try
        {
            await using (var sqlLock = new FileStream(sqlFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var action = () => DatabaseBackupService.BuildPackageSafelyAsync(
                    sqlFile,
                    attachments,
                    packageFile);

                await action.Should().ThrowAsync<IOException>();
                File.Exists(sqlFile).Should().BeTrue("无法删除的明文 SQL 仍会留在磁盘，调用方必须收到失败");
                File.Exists(packageFile).Should().BeFalse("明文 SQL 未清理时不能把 ZIP 报告为成功备份");
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Legacy_sql_cleanup_failure_warns_without_deleting_new_package()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-backup-legacy-lock", Guid.NewGuid().ToString("N"));
        var attachments = Path.Combine(root, "uploads");
        var sqlFile = Path.Combine(root, "assetmgmt_current.sql");
        var packageFile = Path.Combine(root, "assetmgmt_current.zip");
        var legacySqlFile = Path.Combine(root, "assetmgmt_legacy.sql");
        var legacyPackageFile = Path.Combine(root, "assetmgmt_legacy.zip");
        Directory.CreateDirectory(attachments);
        await File.WriteAllTextAsync(sqlFile, "select 1;");
        await File.WriteAllTextAsync(legacySqlFile, "select legacy;");
        await File.WriteAllTextAsync(legacyPackageFile, "legacy package");
        Exception? warning = null;

        try
        {
            await using (var legacyLock = new FileStream(
                             legacySqlFile,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read))
            {
                await DatabaseBackupService.BuildPackageSafelyAsync(
                    sqlFile,
                    attachments,
                    packageFile,
                    legacyCleanupWarning: ex => warning = ex);

                warning.Should().BeOfType<IOException>();
                File.Exists(sqlFile).Should().BeFalse();
                File.Exists(packageFile).Should().BeTrue("历史清理失败不能废弃本次成功备份");
                File.Exists(legacySqlFile).Should().BeTrue();
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
