using AssetManagement.Domain.Services;
using FluentAssertions;

namespace AssetManagement.Tests.Audit;

public class IpNormalizerTests
{
    [Theory]
    [InlineData("::1", "127.0.0.1")]                       // IPv6 回环
    [InlineData("::ffff:192.168.1.5", "192.168.1.5")]      // IPv4-mapped IPv6
    [InlineData("::ffff:10.0.0.1", "10.0.0.1")]
    [InlineData("127.0.0.1", "127.0.0.1")]                 // 已是 IPv4 原样
    [InlineData("192.168.31.42", "192.168.31.42")]
    [InlineData(" 192.168.31.42 ", "192.168.31.42")]       // 去除首尾空白
    public void Normalize_maps_to_ipv4(string raw, string expected)
        => IpNormalizer.Normalize(raw).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_returns_input_for_blank(string? raw)
        => IpNormalizer.Normalize(raw).Should().Be(raw);

    [Fact]
    public void Normalize_keeps_genuine_ipv6()
        => IpNormalizer.Normalize("2001:db8::1").Should().Be("2001:db8::1");

    [Fact]
    public void Normalize_returns_original_for_unparsable()
        => IpNormalizer.Normalize("not-an-ip").Should().Be("not-an-ip");
}
