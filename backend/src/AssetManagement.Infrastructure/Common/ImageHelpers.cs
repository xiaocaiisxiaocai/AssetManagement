using AssetManagement.Application.Common;

namespace AssetManagement.Infrastructure.Common;

internal static class ImageHelpers
{
    internal static string? Join(IEnumerable<string>? images)
    {
        if (images is null) return null;
        var list = images.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList();
        if (list.Count == 0) return null;
        if (list.Count > 5) throw new BizException(4152, "最多上传 5 张照片");
        return string.Join(',', list);
    }

    internal static List<string> Split(string? imageUrls)
        => string.IsNullOrWhiteSpace(imageUrls)
            ? new List<string>()
            : imageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
