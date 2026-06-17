using AssetManagement.Application.Common;
using AssetManagement.Application.Files;

namespace AssetManagement.Infrastructure.Files;

public class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxSizeBytes = 5 * 1024 * 1024;

    private readonly string _root;

    public FileStorageService(string configuredPath, string contentRootPath)
    {
        _root = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<FileUploadResult> SaveImageAsync(Stream content, string originalName, long length)
    {
        var ext = Path.GetExtension(originalName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new BizException(4150, "仅支持 jpg/jpeg/png/gif/webp 图片");
        }
        if (length > MaxSizeBytes)
        {
            throw new BizException(4151, "单张图片大小不能超过 5MB");
        }

        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(_root, name);
        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs);

        return new FileUploadResult { Name = name, Url = $"/api/files/{name}" };
    }

    public StoredFile? Open(string storedName)
    {
        // 防路径穿越:仅接受纯文件名
        if (string.IsNullOrEmpty(storedName)
            || storedName.Contains('/')
            || storedName.Contains('\\')
            || storedName.Contains(".."))
        {
            return null;
        }

        var fullPath = Path.Combine(_root, storedName);
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
}
