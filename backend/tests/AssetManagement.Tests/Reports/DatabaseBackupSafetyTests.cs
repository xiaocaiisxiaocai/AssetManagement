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
}
