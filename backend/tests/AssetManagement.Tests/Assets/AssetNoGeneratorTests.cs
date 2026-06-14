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
}
