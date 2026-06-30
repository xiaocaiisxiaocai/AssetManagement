using System.IO.Compression;
using AssetManagement.Infrastructure.Audit;
using FluentAssertions;

namespace AssetManagement.Tests.Reports;

public class DatabaseBackupPackageTests
{
    [Fact]
    public async Task BuildPackage_includes_database_sql_and_attachments()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-package-test", Guid.NewGuid().ToString("N"));
        var sqlFile = Path.Combine(root, "assetmgmt_20260630_020000.sql");
        var uploadDir = Path.Combine(root, "uploads");
        var packageFile = Path.Combine(root, "assetmgmt_20260630_020000.zip");
        Directory.CreateDirectory(uploadDir);
        await File.WriteAllTextAsync(sqlFile, "create table assets(id int);");
        await File.WriteAllTextAsync(Path.Combine(uploadDir, "asset-photo.png"), "fake image");

        DatabaseBackupPackageBuilder.Build(sqlFile, uploadDir, packageFile);

        using var archive = ZipFile.OpenRead(packageFile);
        archive.Entries.Select(x => x.FullName).Should().Contain(new[]
        {
            "database/assetmgmt_20260630_020000.sql",
            "attachments/asset-photo.png"
        });
    }
}
