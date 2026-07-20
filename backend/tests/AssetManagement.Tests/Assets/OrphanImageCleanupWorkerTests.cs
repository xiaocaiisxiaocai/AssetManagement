using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Files;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetManagement.Tests.Assets;

public class OrphanImageCleanupWorkerTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public OrphanImageCleanupWorkerTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Cleanup_deletes_only_old_unreferenced_valid_images()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-orphan-images", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var referencedName = $"{Guid.NewGuid():N}.png";
        var orphanName = $"{Guid.NewGuid():N}.png";
        var recentName = $"{Guid.NewGuid():N}.png";
        var ignoredName = "manual.png";
        foreach (var name in new[] { referencedName, orphanName, recentName, ignoredName })
        {
            await File.WriteAllBytesAsync(Path.Combine(root, name), new byte[] { 1 });
        }
        File.SetLastWriteTimeUtc(Path.Combine(root, referencedName), DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(Path.Combine(root, orphanName), DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(Path.Combine(root, ignoredName), DateTime.UtcNow.AddDays(-2));

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var category = new AssetCategory
                {
                    CodeSeg = Guid.NewGuid().ToString("N")[..8],
                    Code = Guid.NewGuid().ToString("N"),
                };
                db.AssetCategories.Add(category);
                await db.SaveChangesAsync();
                db.Assets.Add(new Asset
                {
                    AssetNo = Guid.NewGuid().ToString("N"),
                    Name = "引用图片资产",
                    CategoryId = category.Id,
                    ImageUrls = $"/api/files/{referencedName}",
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Attachment:Path"] = root })
                .Build();
            var worker = new OrphanImageCleanupWorker(
                _factory.Services.GetRequiredService<IServiceScopeFactory>(),
                _factory.Services.GetRequiredService<IHostEnvironment>(),
                configuration,
                NullLogger<OrphanImageCleanupWorker>.Instance);

            var deleted = await worker.CleanupAsync(DateTime.UtcNow.AddHours(-24));

            deleted.Should().Be(1);
            File.Exists(Path.Combine(root, orphanName)).Should().BeFalse();
            File.Exists(Path.Combine(root, referencedName)).Should().BeTrue();
            File.Exists(Path.Combine(root, recentName)).Should().BeTrue();
            File.Exists(Path.Combine(root, ignoredName)).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Cleanup_waits_for_reference_commit_and_keeps_newly_referenced_image()
    {
        var root = Path.Combine(Path.GetTempPath(), "assetmgmt-orphan-race", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fileName = $"{Guid.NewGuid():N}.png";
        var path = Path.Combine(root, fileName);
        await File.WriteAllBytesAsync(path, new byte[] { 1 });
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Attachment:Path"] = root })
                .Build();
            var worker = new OrphanImageCleanupWorker(
                _factory.Services.GetRequiredService<IServiceScopeFactory>(),
                _factory.Services.GetRequiredService<IHostEnvironment>(),
                configuration,
                NullLogger<OrphanImageCleanupWorker>.Instance);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storage = new FileStorageService(root, root, db);
            await using var referenceLease = await storage.AcquireReferenceLeaseAsync(
                new[] { $"/api/files/{fileName}" });

            var cleanupTask = worker.CleanupAsync(DateTime.UtcNow.AddHours(-24));
            await Task.Delay(100);
            cleanupTask.IsCompleted.Should().BeFalse();

            var category = new AssetCategory
            {
                CodeSeg = Guid.NewGuid().ToString("N")[..8],
                Code = Guid.NewGuid().ToString("N"),
            };
            db.AssetCategories.Add(category);
            await db.SaveChangesAsync();
            db.Assets.Add(new Asset
            {
                AssetNo = Guid.NewGuid().ToString("N"),
                Name = "并发建立图片引用",
                CategoryId = category.Id,
                ImageUrls = $"/api/files/{fileName}",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            await referenceLease.DisposeAsync();
            var deleted = await cleanupTask;

            deleted.Should().Be(0);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
