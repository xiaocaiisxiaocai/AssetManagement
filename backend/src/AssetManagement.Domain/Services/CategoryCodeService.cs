namespace AssetManagement.Domain.Services;

public interface ICatNode
{
    string Seg { get; set; }
    string Code { get; set; }
    IEnumerable<ICatNode> Children { get; }
}

public static class CategoryCodeService
{
    public static string Compose(string? parentCode, string seg)
    {
        var normalizedSeg = seg.Trim();
        return string.IsNullOrWhiteSpace(parentCode)
            ? normalizedSeg
            : $"{parentCode.Trim()}-{normalizedSeg}";
    }

    public static void Recalc(ICatNode node, string? parentCode)
    {
        node.Code = Compose(parentCode, node.Seg);
        foreach (var child in node.Children)
        {
            Recalc(child, node.Code);
        }
    }
}
