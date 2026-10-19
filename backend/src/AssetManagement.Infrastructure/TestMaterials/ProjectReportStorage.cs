using System.IO.Compression;
using System.Text.RegularExpressions;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AssetManagement.Infrastructure.TestMaterials;

/// <summary>项目报告独立存放于附件根目录的子目录，随数据库备份归档，不参与图片清理。</summary>
public class ProjectReportStorage
{
    public const long MaxFileSize = 20 * 1024 * 1024;
    private readonly string _root;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg"
    };

    public ProjectReportStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Attachment:Path"] ?? "App_Data/uploads";
        var attachmentRoot = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured : Path.Combine(environment.ContentRootPath, configured));
        _root = Path.Combine(attachmentRoot, "project-reports");
    }

    public async Task<(string StoredName, string FileName, string ContentType, long Size)> SaveAsync(
        string originalName, long length, Stream content)
    {
        var fileName = Path.GetFileName(originalName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 200 ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.Any(char.IsControl))
            throw new BizException(4001, "报告文件名无效或超过 200 个字符");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!Types.TryGetValue(extension, out var contentType))
            throw new BizException(4150, "报告支持 PDF、DOCX、XLSX、PPTX、PNG、JPG 格式");
        if (length <= 0 || length > MaxFileSize)
            throw new BizException(4151, "报告不能为空，单个文件不能超过 20MB");

        Directory.CreateDirectory(_root);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = Resolve(storedName);
        try
        {
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long count = 0;
                int read;
                while ((read = await content.ReadAsync(buffer)) > 0)
                {
                    count += read;
                    if (count > MaxFileSize || count > length)
                        throw new BizException(4151, "报告文件实际大小超过限制");
                    await target.WriteAsync(buffer.AsMemory(0, read));
                }
                if (count != length) throw new BizException(4150, "报告上传不完整，请重新上传");
            }
            ValidateFormat(path, extension);
            return (storedName, fileName, contentType, length);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public Stream Open(string storedName)
    {
        var path = Resolve(storedName);
        if (!File.Exists(path)) throw new BizException(4046, "报告文件不存在，请重新上传");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void RemoveUncommitted(string storedName) => File.Delete(Resolve(storedName));

    private string Resolve(string storedName)
    {
        if (!Regex.IsMatch(storedName, @"^[0-9a-f]{32}\.(pdf|docx|xlsx|pptx|png|jpg|jpeg)$", RegexOptions.CultureInvariant))
            throw new BizException(4001, "报告文件标识无效");
        return Path.Combine(_root, storedName);
    }

    private static void ValidateFormat(string path, string extension)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        var size = file.Read(header);
        if (extension == ".pdf")
        {
            if (size < 5 || !header[..5].SequenceEqual("%PDF-"u8))
                throw new BizException(4150, "文件内容与 PDF 格式不匹配");
            return;
        }
        if (extension is ".png" or ".jpg" or ".jpeg")
        {
            if (!FileStorageService.MatchesImageSignature(extension, header[..size]))
                throw new BizException(4150, "文件内容与图片格式不匹配");
            return;
        }

        file.Position = 0;
        try
        {
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            var mainPart = extension switch
            {
                ".docx" => "word/document.xml", ".xlsx" => "xl/workbook.xml", _ => "ppt/presentation.xml"
            };
            if (archive.Entries.Count > 10000 ||
                archive.Entries.Sum(x => x.Length) > 200 * 1024L * 1024L ||
                archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry(mainPart) is null ||
                archive.Entries.Any(x => x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                throw new BizException(4150, "Office 报告格式无效、内容过大或包含宏");
        }
        catch (InvalidDataException)
        {
            throw new BizException(4150, "文件内容与 Office 报告格式不匹配");
        }
    }
}
