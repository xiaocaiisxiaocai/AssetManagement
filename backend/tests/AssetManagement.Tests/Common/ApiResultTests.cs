using AssetManagement.Application.Common;
using FluentAssertions;
using Xunit;

public class ApiResultTests
{
    [Fact]
    public void Ok_sets_code_zero_and_data()
    {
        var r = ApiResult<int>.Ok(42);

        r.Code.Should().Be(0);
        r.Data.Should().Be(42);
    }

    [Fact]
    public void Fail_sets_code_and_message()
    {
        var r = ApiResult<int>.Fail(4001, "无权访问");

        r.Code.Should().Be(4001);
        r.Message.Should().Be("无权访问");
    }
}
