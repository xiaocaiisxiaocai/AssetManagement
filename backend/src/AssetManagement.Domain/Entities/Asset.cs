namespace AssetManagement.Domain.Entities;

public enum AssetStatus
{
    Available = 0,
    Borrowed = 1,
    Maintenance = 2,
    Scrapped = 3
}

public class Asset
{
    public int Id { get; set; }
    public string AssetNo { get; set; } = "";
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public int? LocationId { get; set; }
    public int? CustodianId { get; set; }
    public string? Model { get; set; }
    public string? Brand { get; set; }
    public int Quantity { get; set; } = 1;
    public AssetStatus Status { get; set; } = AssetStatus.Available;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>资产照片附件 URL,逗号分隔(最多 5 张),由 /api/files 上传得到</summary>
    public string? ImageUrls { get; set; }
}
