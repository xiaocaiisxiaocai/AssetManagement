using AssetManagement.Application.Common;
using AssetManagement.Application.Files;
using AssetManagement.Infrastructure.Common;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Files;

public class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly AppDbContext _db;
    private readonly string _root;

    public FileStorageService(string configuredPath, string contentRootPath, AppDbContext db)
    {
        _db = db;
        _root = NormalizeRoot(Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath)));
        Directory.CreateDirectory(_root);
    }

    public async Task<FileUploadResult> SaveImageAsync(Stream content, string originalName, long length)
    {
        var ext = Path.GetExtension(originalName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new BizException(4150, "仅支持 jpg/jpeg/png/gif/webp 图片");
        }
        var maxMb = await LoadAttachmentMaxMbAsync();
        if (length > maxMb * 1024L * 1024L)
        {
            throw new BizException(4151, $"单张图片大小不能超过 {maxMb}MB");
        }
        if (length <= 0) throw new BizException(4150, "图片内容为空");

        var header = new byte[12];
        var headerLength = 0;
        while (headerLength < header.Length)
        {
            var read = await content.ReadAsync(header.AsMemory(headerLength, header.Length - headerLength));
            if (read == 0) break;
            headerLength += read;
        }
        if (!MatchesImageSignature(ext, header.AsSpan(0, headerLength)))
            throw new BizException(4150, "文件内容与图片格式不匹配");

        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(_root, name);
        try
        {
            await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await fs.WriteAsync(header.AsMemory(0, headerLength));
            await content.CopyToAsync(fs);
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }

        return new FileUploadResult { Name = name, Url = $"/api/files/{name}" };
    }

    public async Task<IAsyncDisposable> AcquireReferenceLeaseAsync(
        IEnumerable<string>? imageUrls,
        CancellationToken cancellationToken = default)
    {
        var lease = await ImageLifecycleLock.AcquireAsync(cancellationToken);
        try
        {
            foreach (var url in imageUrls ?? Array.Empty<string>())
            {
                if (!ImageHelpers.IsStoredImageUrl(url))
                {
                    throw new BizException(4152, "照片地址无效，仅允许使用本系统上传的图片");
                }

                var storedName = Path.GetFileName(url);
                var fullPath = Path.GetFullPath(Path.Combine(_root, storedName));
                if (!IsPathInsideRoot(fullPath) || !File.Exists(fullPath))
                {
                    throw new BizException(4152, "照片文件不存在或已被清理，请重新上传");
                }
            }

            return lease;
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    public StoredFile? Open(string storedName)
    {
        // 防路径穿越:仅接受纯文件名
        if (!ImageHelpers.IsStoredImageName(storedName))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, storedName));
        if (!IsPathInsideRoot(fullPath))
        {
            return null;
        }
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new StoredFile(stream, ContentTypeFor(Path.GetExtension(storedName)));
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    internal static bool MatchesImageSignature(string extension, ReadOnlySpan<byte> header)
    {
        extension = extension.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".gif" => header.Length >= 6 &&
                      (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)),
            ".webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private async Task<int> LoadAttachmentMaxMbAsync()
    {
        var value = await _db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == "attachment_max_mb")
            .Select(x => x.Value)
            .SingleOrDefaultAsync();

        return int.TryParse(value, out var maxMb)
            ? Math.Clamp(maxMb, 1, 100)
            : 5;
    }

    private static string NormalizeRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
            ? path
            : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private bool IsPathInsideRoot(string fullPath)
    {
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
            || _root.EndsWith(Path.AltDirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
