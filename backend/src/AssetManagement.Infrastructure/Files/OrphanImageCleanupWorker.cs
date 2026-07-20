using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Files;

/// <summary>回收上传超过 24 小时且没有被资产或测试料件引用的图片。</summary>
public sealed class OrphanImageCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrphanImageCleanupWorker> _logger;

    public OrphanImageCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<OrphanImageCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(DateTime.UtcNow.AddHours(-24), stoppingToken);
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "孤儿图片清理失败，将在一小时后重试");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    internal async Task<int> CleanupAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        // 与业务保存的“验证文件 + 提交引用”共享一把锁。
        // 若清理先拿到锁，保存时会发现文件已不存在；若保存先拿到锁，清理会读到新引用。
        await using var lifecycleLease = await ImageLifecycleLock.AcquireAsync(cancellationToken);
        var root = ResolveRoot();
        if (!Directory.Exists(root))
        {
            return 0;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var imageColumns = await db.Assets.AsNoTracking()
            .Where(x => x.ImageUrls != null)
            .Select(x => x.ImageUrls!)
            .Concat(db.TestMaterials.AsNoTracking()
                .Where(x => x.ImageUrls != null)
                .Select(x => x.ImageUrls!))
            .ToListAsync(cancellationToken);
        var referencedNames = imageColumns
            .SelectMany(ImageHelpers.Split)
            .Where(ImageHelpers.IsStoredImageUrl)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(path);
            if (!ImageHelpers.IsStoredImageName(fileName)
                || referencedNames.Contains(fileName)
                || File.GetLastWriteTimeUtc(path) >= olderThanUtc)
            {
                continue;
            }

            try
            {
                File.Delete(path);
                deleted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除孤儿图片失败：{FileName}", fileName);
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("已回收孤儿图片 {Count} 个", deleted);
        }
        return deleted;
    }

    private string ResolveRoot()
    {
        var configured = _configuration["Attachment:Path"] ?? "App_Data/uploads";
        return Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured));
    }
}
