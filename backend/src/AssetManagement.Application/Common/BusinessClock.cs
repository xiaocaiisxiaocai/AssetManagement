namespace AssetManagement.Application.Common;

/// <summary>
/// 业务日期统一使用中国标准时间，避免服务器部署时区不同导致日期边界错乱。
/// </summary>
public static class BusinessClock
{
    private static readonly TimeZoneInfo ChinaTimeZone = ResolveChinaTimeZone();

    public static DateTime Now
        => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ChinaTimeZone).DateTime;

    public static DateTime Today => Now.Date;

    public static DateOnly TodayDateOnly => DateOnly.FromDateTime(Now);

    private static TimeZoneInfo ResolveChinaTimeZone()
    {
        foreach (var id in new[] { "China Standard Time", "Asia/Shanghai" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // 尝试下一个跨平台时区标识。
            }
            catch (InvalidTimeZoneException)
            {
                // 尝试下一个跨平台时区标识。
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "UTC+08:00",
            TimeSpan.FromHours(8),
            "中国标准时间",
            "中国标准时间");
    }
}
