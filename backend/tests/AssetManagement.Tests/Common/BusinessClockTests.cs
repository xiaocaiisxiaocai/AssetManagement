using AssetManagement.Application.Common;
using FluentAssertions;

namespace AssetManagement.Tests.Common;

public class BusinessClockTests
{
    [Fact]
    public void China_day_boundary_is_converted_to_utc_without_using_server_timezone()
    {
        var chinaMidnight = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Unspecified);

        var utc = BusinessClock.ToUtc(chinaMidnight);

        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Should().Be(new DateTime(2026, 7, 9, 16, 0, 0, DateTimeKind.Utc));
        BusinessClock.FromUtc(utc).Should().Be(chinaMidnight);
    }
}
