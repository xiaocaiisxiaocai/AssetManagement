using System.IO.Compression;

namespace AssetManagement.Infrastructure.Audit;

public static class DatabaseBackupPackageBuilder
{
    public static void Build(string sqlFilePath, string attachmentPath, string packageFilePath)
    {
        var tempPackagePath = $"{packageFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var archive = ZipFile.Open(tempPackagePath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(sqlFilePath, $"database/{Path.GetFileName(sqlFilePath)}", CompressionLevel.Optimal);

                if (Directory.Exists(attachmentPath))
                {
                    foreach (var file in Directory.EnumerateFiles(attachmentPath, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(attachmentPath, file).Replace('\\', '/');
                        archive.CreateEntryFromFile(file, $"attachments/{relativePath}", CompressionLevel.Optimal);
                    }
                }
            }

            File.Move(tempPackagePath, packageFilePath, true);
        }
        finally
        {
            if (File.Exists(tempPackagePath)) File.Delete(tempPackagePath);
        }
    }
}
