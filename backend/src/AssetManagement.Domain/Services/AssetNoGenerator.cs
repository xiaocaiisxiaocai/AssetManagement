using System.Globalization;

namespace AssetManagement.Domain.Services;

public static class AssetNoGenerator
{
    public static string Next(string categoryCode, int existingCount)
        => $"{categoryCode}-{existingCount + 1:D3}";

    /// <summary>
    /// 只统计「分类编码-纯数字」编号里的最大流水。自定义编号、前导零以外的文本后缀不参与取号。
    /// </summary>
    public static int MaxSequence(string categoryCode, IEnumerable<string> assetNos)
    {
        var prefix = $"{categoryCode}-";
        var max = 0;
        foreach (var assetNo in assetNos)
        {
            if (string.IsNullOrEmpty(assetNo)
                || !assetNo.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = assetNo[prefix.Length..];
            if (suffix.Length == 0 || suffix.Any(ch => !char.IsAsciiDigit(ch)))
            {
                continue;
            }

            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                && sequence > max)
            {
                max = sequence;
            }
        }

        return max;
    }
}
