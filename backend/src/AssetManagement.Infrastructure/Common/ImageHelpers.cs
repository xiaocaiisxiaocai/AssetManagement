using AssetManagement.Application.Common;
using System.Text.RegularExpressions;

namespace AssetManagement.Infrastructure.Common;

internal static class ImageHelpers
{
    private static readonly Regex StoredImageUrlPattern = new(
        @"^/api/files/(?<name>[0-9a-f]{32}\.(?:jpe?g|png|gif|webp))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static string? Join(IEnumerable<string>? images)
    {
        if (images is null) return null;
        var list = images.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList();
        if (list.Count == 0) return null;
        if (list.Count > 5) throw new BizException(4152, "最多上传 5 张照片");
        if (list.Any(x => !IsStoredImageUrl(x)))
            throw new BizException(4152, "照片地址无效，仅允许使用本系统上传的图片");
        var joined = string.Join(',', list);
        if (joined.Length > 2000)
            throw new BizException(4152, "照片地址总长度超出限制");
        return joined;
    }

    internal static List<string> Split(string? imageUrls)
        => string.IsNullOrWhiteSpace(imageUrls)
            ? new List<string>()
            : imageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    internal static bool IsStoredImageUrl(string value)
        => !string.IsNullOrWhiteSpace(value) && StoredImageUrlPattern.IsMatch(value);

    internal static bool IsStoredImageName(string value)
        => !string.IsNullOrWhiteSpace(value)
           && StoredImageUrlPattern.IsMatch($"/api/files/{value}");
}
