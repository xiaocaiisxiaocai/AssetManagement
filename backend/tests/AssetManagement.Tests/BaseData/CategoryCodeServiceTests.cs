using AssetManagement.Domain.Services;
using FluentAssertions;

namespace AssetManagement.Tests.BaseData;

public class CategoryCodeServiceTests
{
    [Fact]
    public void Compose_top_level_equals_seg()
        => CategoryCodeService.Compose(parentCode: null, seg: "PLC").Should().Be("PLC");

    [Fact]
    public void Compose_child_joins_with_dash()
        => CategoryCodeService.Compose("PLC", "MIT").Should().Be("PLC-MIT");

    [Fact]
    public void Recalc_updates_descendants()
    {
        var root = new CatNode("PLC", "PLC")
        {
            Children =
            [
                new CatNode("MIT", "PLC-MIT")
                {
                    Children = [new CatNode("Q", "PLC-MIT-Q")]
                }
            ]
        };

        CategoryCodeService.Recalc(root, parentCode: null);
        root.Children[0].Children[0].Code.Should().Be("PLC-MIT-Q");

        root.Seg = "PLCX";
        CategoryCodeService.Recalc(root, parentCode: null);

        root.Code.Should().Be("PLCX");
        root.Children[0].Code.Should().Be("PLCX-MIT");
        root.Children[0].Children[0].Code.Should().Be("PLCX-MIT-Q");
    }

    private sealed class CatNode : ICatNode
    {
        public CatNode(string seg, string code)
        {
            Seg = seg;
            Code = code;
        }

        public string Seg { get; set; }
        public string Code { get; set; }
        public List<CatNode> Children { get; set; } = new();
        IEnumerable<ICatNode> ICatNode.Children => Children;
    }
}
