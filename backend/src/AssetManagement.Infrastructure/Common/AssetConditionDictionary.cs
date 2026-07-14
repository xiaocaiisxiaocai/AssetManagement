using System.Text.Json;
using AssetManagement.Application.Common;

namespace AssetManagement.Infrastructure.Common;

internal static class AssetConditionDictionary
{
    public const string SettingKey = "asset_condition_options";
    public const string DefaultSerializedValue = "[\"正常使用\",\"轻微损坏\",\"待维修\",\"维修中\",\"停用\"]";

    public static IReadOnlyList<string> ParseOrDefault(string? raw)
        => TryParse(raw, out var options) ? options : ParseDefaults();

    public static string NormalizeSettingValue(string raw)
    {
        if (!TryParse(raw, out var options))
        {
            throw new BizException(4001, "系统参数「asset_condition_options」必须包含 1-20 个不重复的状况选项，每项不超过 50 个字符");
        }

        return JsonSerializer.Serialize(options);
    }

    public static string? NormalizeSelection(
        string? value,
        IReadOnlyList<string> options,
        string? existingValue = null)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate)) return null;

        var canonical = options.FirstOrDefault(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        if (canonical is not null) return canonical;

        var existing = existingValue?.Trim();
        if (!string.IsNullOrEmpty(existing)
            && existing.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        {
            return existing;
        }

        throw new BizException(4001, $"目前状况「{candidate}」不在数据字典中");
    }

    private static bool TryParse(string? raw, out List<string> options)
    {
        options = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return false;

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(raw);
            if (values is null || values.Count is < 1 or > 20) return false;

            options = values.Select(x => x?.Trim() ?? string.Empty).ToList();
            return options.All(x => x.Length is > 0 and <= 50)
                   && options.Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.Count;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<string> ParseDefaults()
        => JsonSerializer.Deserialize<List<string>>(DefaultSerializedValue)!;
}
