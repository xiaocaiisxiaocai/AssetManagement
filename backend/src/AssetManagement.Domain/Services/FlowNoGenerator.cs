namespace AssetManagement.Domain.Services;

public static class FlowNoGenerator
{
    /// <summary>流转单号: MF-yyyyMMdd-NNN(当日序号,从 001 起,超过 999 自然增长位数)</summary>
    public static string Next(DateTime date, int existingCountToday)
        => $"MF-{date:yyyyMMdd}-{existingCountToday + 1:D3}";
}
