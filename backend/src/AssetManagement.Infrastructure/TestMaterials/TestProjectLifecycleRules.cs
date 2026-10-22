namespace AssetManagement.Infrastructure.TestMaterials;

internal static class TestProjectLifecycleRules
{
    public const string Planning = "planning";
    public const string Testing = "testing";
    public const string Landing = "landing";
    public const string Closed = "closed";

    public static bool IsTerminal(string? progressCode) =>
        Equals(progressCode, Landing) || Equals(progressCode, Closed);

    public static bool IsMaterialMaintainable(
        string? progressCode,
        DateTime? closedDate) =>
        !closedDate.HasValue &&
        (Equals(progressCode, Planning) || Equals(progressCode, Testing));

    public static bool IsTestingStageMaintainable(
        string? progressCode,
        DateTime? closedDate) =>
        !closedDate.HasValue && Equals(progressCode, Testing);

    public static bool IsInitialProjectProgressAllowed(string? progressCode) =>
        Equals(progressCode, Planning) || Equals(progressCode, Testing);

    public static bool IsSupportedProgress(string? progressCode) =>
        Equals(progressCode, Planning) || Equals(progressCode, Testing) ||
        Equals(progressCode, Landing) || Equals(progressCode, Closed);

    /// <summary>
    /// 计划中只能留在计划中或进入测试中；测试中可进入落地跟进或结案；
    /// 落地跟进只能结案；已结案保持锁定。未知进度只能回到计划中或测试中。
    /// </summary>
    public static bool CanTransition(string? currentProgressCode, string? nextProgressCode)
    {
        if (!IsSupportedProgress(nextProgressCode) || nextProgressCode is null) return false;
        if (!IsSupportedProgress(currentProgressCode))
            return IsInitialProjectProgressAllowed(nextProgressCode);
        if (Equals(currentProgressCode, nextProgressCode)) return true;
        if (Equals(currentProgressCode, Planning)) return Equals(nextProgressCode, Testing);
        if (Equals(currentProgressCode, Testing))
            return Equals(nextProgressCode, Landing) || Equals(nextProgressCode, Closed);
        if (Equals(currentProgressCode, Landing)) return Equals(nextProgressCode, Closed);
        return false;
    }

    private static bool Equals(string? actual, string expected) =>
        string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}
