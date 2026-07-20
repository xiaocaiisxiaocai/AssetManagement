using AssetManagement.Application.Common;

namespace AssetManagement.Application.Files;

/// <summary>
/// 通用文件存储:当前用于资产照片附件。文件落地到可配置的本地目录,
/// 通过 /api/files/{name} 读取(读写均需相应权限)。
/// </summary>
public interface IFileStorageService
{
    Task<FileUploadResult> SaveImageAsync(Stream content, string originalName, long length);
    /// <summary>
    /// 在业务实体持久化期间锁定图片生命周期，并确认每个 URL 都对应真实存储文件。
    /// 调用方必须将返回的 lease 保持到数据库事务提交完成，防止孤儿清理与建立引用交错。
    /// </summary>
    Task<IAsyncDisposable> AcquireReferenceLeaseAsync(
        IEnumerable<string>? imageUrls,
        CancellationToken cancellationToken = default);
    StoredFile? Open(string storedName);
}

public record FileUploadResult
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
}

public record StoredFile(Stream Stream, string ContentType);
