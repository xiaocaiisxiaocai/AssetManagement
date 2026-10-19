namespace AssetManagement.Domain.Entities;

public class ProjectReportDocument
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? StageId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string? Description { get; set; }
    public int UploadedById { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public bool IsDeleted { get; set; }
}
