using AssetManagement.Domain.Services;
using FluentAssertions;

namespace AssetManagement.Tests.Assets;

public class AssetNoGeneratorTests
{
    [Theory]
    [InlineData("PLC-MIT-Q", 0, "PLC-MIT-Q-001")]
    [InlineData("PLC-MIT-Q", 2, "PLC-MIT-Q-003")]
    [InlineData("TOOL-HAM", 9, "TOOL-HAM-010")]
    public void Next_appends_zero_padded_sequence(string categoryCode, int existingCount, string expected)
        => AssetNoGenerator.Next(categoryCode, existingCount).Should().Be(expected);

    [Fact]
    public void MaxSequence_ignores_custom_text_and_uses_numeric_suffix()
    {
        var assetNos = new[]
        {
            "IT-旧台账-015",
            "IT-0007",
            "IT-012",
            "OTHER-099",
            "IT-",
            "IT-12A",
        };

        AssetNoGenerator.MaxSequence("IT", assetNos).Should().Be(12);
    }

    [Fact]
    public void MaxSequence_returns_zero_when_no_numeric_suffix_exists()
        => AssetNoGenerator.MaxSequence("IT", ["IT-旧台账", "XX-001"]).Should().Be(0);
}
