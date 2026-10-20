using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.TestMaterials;

public class ProjectWorkspaceDto
{
    public int ProjectId { get; set; }
    public bool CanManage { get; set; }
    public List<ProjectTestStageDto> Stages { get; set; } = new();
    public List<ProjectReportDocumentDto> Documents { get; set; } = new();
}

public class ProjectTestStageDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = "";
    public string? Objective { get; set; }
    public string? TestContent { get; set; }
    public string? TestResult { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "planned";
    public uint RowVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveProjectTestStageRequest
{
    [Range(1, 999)]
    public int Sequence { get; set; } = 1;
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    [StringLength(2000)]
    public string? Objective { get; set; }
    [StringLength(8000)]
    public string? TestContent { get; set; }
    [StringLength(8000)]
    public string? TestResult { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Required, StringLength(20)]
    public string Status { get; set; } = "planned";
    public uint RowVersion { get; set; }
}

public class ProjectReportDocumentDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? StageId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string? Description { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}

public class ProjectReportData
{
    public int? StageId { get; set; }
    public TestProjectDto Project { get; set; } = new();
    public List<TestMaterialDto> Materials { get; set; } = new();
    public List<ProjectTestStageDto> Stages { get; set; } = new();
    public List<ProjectReportDocumentDto> Documents { get; set; } = new();
    public List<TestProjectFollowupDto> Followups { get; set; } = new();
    public List<MaterialFlowDto> Flows { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public record ProjectWorkspaceFile(Stream Stream, string ContentType, string FileName);

public interface IProjectWorkspaceService
{
    Task<ProjectWorkspaceDto> GetAsync(int projectId, int userId);
    Task<ProjectTestStageDto> SaveStageAsync(int projectId, int? stageId, SaveProjectTestStageRequest request, int userId);
    Task DeleteStageAsync(int projectId, int stageId, uint rowVersion, int userId);
    Task<ProjectReportDocumentDto> UploadAsync(int projectId, int? stageId, string? description, string fileName, long length, Stream content, int userId);
    Task<ProjectWorkspaceFile> DownloadAsync(int projectId, int documentId);
    Task DeleteDocumentAsync(int projectId, int documentId, int userId);
    Task<ProjectWorkspaceFile> ExportAsync(int projectId, int? stageId, bool package, int userId);
}
