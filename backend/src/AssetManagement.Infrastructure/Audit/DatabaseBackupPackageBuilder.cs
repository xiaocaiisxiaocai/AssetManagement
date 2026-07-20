using System.IO.Compression;

namespace AssetManagement.Infrastructure.Audit;

public static class DatabaseBackupPackageBuilder
{
    public static async Task BuildAsync(
        string sqlFilePath,
        string attachmentPath,
        string packageFilePath,
        CancellationToken cancellationToken = default)
    {
        var tempPackagePath = $"{packageFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var archive = ZipFile.Open(tempPackagePath, ZipArchiveMode.Create))
            {
                await AddFileAsync(
                    archive,
                    sqlFilePath,
                    $"database/{Path.GetFileName(sqlFilePath)}",
                    cancellationToken);

                if (Directory.Exists(attachmentPath))
                {
                    foreach (var file in Directory.EnumerateFiles(attachmentPath, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var relativePath = Path.GetRelativePath(attachmentPath, file).Replace('\\', '/');
                        await AddFileAsync(archive, file, $"attachments/{relativePath}", cancellationToken);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPackagePath, packageFilePath, true);
        }
        finally
        {
            if (File.Exists(tempPackagePath)) File.Delete(tempPackagePath);
        }
    }

    private static async Task AddFileAsync(
        ZipArchive archive,
        string sourcePath,
        string entryName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await using var destination = entry.Open();
        await source.CopyToAsync(destination, 81920, cancellationToken);
    }
}
