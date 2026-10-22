namespace AssetManagement.Application.Assets;

public record AssetOverviewDto
{
    public int Total { get; init; }
    public int Available { get; init; }
    public int Borrowed { get; init; }
    public int Maintenance { get; init; }
    public int Scrapped { get; init; }
    public int UncategorizedTotal { get; init; }
    public List<AssetOverviewCategoryDto> Categories { get; init; } = new();
}

public record AssetOverviewCategoryDto
{
    public int CategoryId { get; init; }
    public string CategoryCode { get; init; } = "";
    public string Label { get; init; } = "";
    public string ParentCode { get; init; } = "";
    public string? Remark { get; init; }
    public int Total { get; init; }
    public int Available { get; init; }
    public int Borrowed { get; init; }
    public int Maintenance { get; init; }
    public int Scrapped { get; init; }
}

public interface IAssetOverviewService
{
    Task<AssetOverviewDto> GetOverviewAsync();
}
