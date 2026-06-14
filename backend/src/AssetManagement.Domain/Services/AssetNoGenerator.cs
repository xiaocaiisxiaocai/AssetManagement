namespace AssetManagement.Domain.Services;

public static class AssetNoGenerator
{
    public static string Next(string categoryCode, int existingCount)
        => $"{categoryCode}-{existingCount + 1:D3}";
}
