using AssetManagement.Domain.Services;

namespace AssetManagement.Domain.Entities;

public class AssetCategory : ICatNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string CodeSeg { get; set; } = "";
    public string Code { get; set; } = "";
    public List<AssetCategory> Children { get; set; } = new();

    string ICatNode.Seg
    {
        get => CodeSeg;
        set => CodeSeg = value;
    }

    IEnumerable<ICatNode> ICatNode.Children => Children;
}
