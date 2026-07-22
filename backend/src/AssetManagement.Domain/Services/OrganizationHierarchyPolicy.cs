namespace AssetManagement.Domain.Services;

public static class OrganizationHierarchyPolicy
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedChildren =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["company"] = ["division", "department"],
            ["division"] = ["department", "section"],
            ["department"] = ["section"],
            ["section"] = []
        };

    private static readonly IReadOnlyDictionary<string, string> LevelNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["company"] = "公司/中心",
            ["division"] = "事业部",
            ["department"] = "部门",
            ["section"] = "课别"
        };

    public static IReadOnlyList<string> GetAllowedChildCodes(string parentLevelCode)
        => AllowedChildren.TryGetValue(parentLevelCode, out var children)
            ? children
            : [];

    public static string? GetDefaultChildCode(string parentLevelCode)
        => GetAllowedChildCodes(parentLevelCode).FirstOrDefault();

    public static bool CanContain(string parentLevelCode, string childLevelCode)
        => GetAllowedChildCodes(parentLevelCode)
            .Contains(childLevelCode, StringComparer.OrdinalIgnoreCase);

    public static string GetLevelName(string levelCode)
        => LevelNames.GetValueOrDefault(levelCode, levelCode);
}
