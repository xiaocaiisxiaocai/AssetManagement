using System.IO.Compression;

namespace AssetManagement.Infrastructure.Audit;

public static class DatabaseBackupPackageBuilder
{
    public static void Build(string sqlFilePath, string attachmentPath, string packageFilePath)
    {
        if (File.Exists(packageFilePath))
        {
            File.Delete(packageFilePath);
        }

        using var archive = ZipFile.Open(packageFilePath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(sqlFilePath, $"database/{Path.GetFileName(sqlFilePath)}", CompressionLevel.Optimal);

        if (!Directory.Exists(attachmentPath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(attachmentPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(attachmentPath, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"attachments/{relativePath}", CompressionLevel.Optimal);
        }
    }
}
