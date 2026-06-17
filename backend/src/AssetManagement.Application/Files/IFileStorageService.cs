using AssetManagement.Application.Common;

namespace AssetManagement.Application.Files;

/// <summary>
/// 通用文件存储:当前用于资产照片附件。文件落地到可配置的本地目录,
/// 通过 /api/files/{name} 读取(读取匿名,文件名为随机 GUID 不可枚举;上传需权限)。
/// </summary>
public interface IFileStorageService
{
    Task<FileUploadResult> SaveImageAsync(Stream content, string originalName, long length);
    StoredFile? Open(string storedName);
}

public record FileUploadResult
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
}

public record StoredFile(Stream Stream, string ContentType);
