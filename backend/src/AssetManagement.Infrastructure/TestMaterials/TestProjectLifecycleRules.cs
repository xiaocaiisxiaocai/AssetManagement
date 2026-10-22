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

    private static bool Equals(string? actual, string expected) =>
        string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}
