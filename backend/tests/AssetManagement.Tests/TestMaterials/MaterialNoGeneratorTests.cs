using AssetManagement.Domain.Services;
using FluentAssertions;
using Xunit;

namespace AssetManagement.Tests.TestMaterials;

public class MaterialNoGeneratorTests
{
    [Fact]
    public void Next_formats_with_prefix_date_and_padded_sequence()
    {
        var date = new DateTime(2026, 6, 25);
        MaterialNoGenerator.Next(date, 0).Should().Be("TM-20260625-001");
        MaterialNoGenerator.Next(date, 5).Should().Be("TM-20260625-006");
    }

    [Fact]
    public void Next_pads_to_three_digits_then_grows()
    {
        var date = new DateTime(2026, 6, 25);
        MaterialNoGenerator.Next(date, 999).Should().Be("TM-20260625-1000");
    }
}
