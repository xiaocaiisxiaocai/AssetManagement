namespace AssetManagement.Domain.Services;

public static class MaterialNoGenerator
{
    /// <summary>测试料件临时编号: TM-yyyyMMdd-NNN(当日序号,从 001 起,超过 999 自然增长位数)</summary>
    public static string Next(DateTime date, int existingCountToday)
        => $"TM-{date:yyyyMMdd}-{existingCountToday + 1:D3}";
}
