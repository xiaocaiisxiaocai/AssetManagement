namespace AssetManagement.Domain.Entities;

public enum MaterialStatus
{
    InUse = 0,            // 在用
    ReturnedToVendor = 1  // 已退回厂商
}

public class TestMaterial
{
    public int Id { get; set; }
    public string MaterialNo { get; set; } = "";
    public string Name { get; set; } = "";
    public int ProjectId { get; set; }
    public string? VendorName { get; set; }
    public string? Model { get; set; }
    public string? Brand { get; set; }
    public int Quantity { get; set; } = 1;
    public int? DepartmentId { get; set; }
    public int? LocationId { get; set; }
    public int? CustodianId { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public MaterialStatus Status { get; set; } = MaterialStatus.InUse;
    /// <summary>料件照片附件 URL,逗号分隔(最多 5 张),由 /api/files 上传得到</summary>
    public string? ImageUrls { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
