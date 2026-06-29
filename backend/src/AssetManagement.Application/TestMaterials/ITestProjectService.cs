namespace AssetManagement.Application.TestMaterials;

public interface ITestProjectService
{
    Task<List<TestProjectDto>> ListAsync(string? deleteStatus, int currentUserId);
    Task<TestProjectDto> CreateAsync(SaveTestProjectRequest request);
    Task<TestProjectDto> UpdateAsync(int id, SaveTestProjectRequest request);
    Task DeleteAsync(int id);
    Task RestoreAsync(int id);
    Task PurgeAsync(int id);
    Task<List<TestProjectOptionDto>> ListOptionsAsync(string? kind);
    Task<TestProjectOptionDto> CreateOptionAsync(SaveTestProjectOptionRequest request);
    Task<TestProjectOptionDto> UpdateOptionAsync(int id, SaveTestProjectOptionRequest request);
    Task DeleteOptionAsync(int id);
    Task<List<TestProjectFollowupDto>> ListFollowupsAsync(int projectId);
    Task<TestProjectFollowupDto> CreateFollowupAsync(int projectId, SaveTestProjectFollowupRequest request, int currentUserId);
    Task<TestProjectFollowupDto> UpdateFollowupAsync(int projectId, int followupId, SaveTestProjectFollowupRequest request, int currentUserId);
    Task DeleteFollowupAsync(int projectId, int followupId, int currentUserId);
    Task<TestProjectStatsDto> GetStatsAsync();
}

public class TestProjectStatsDto
{
    public int Total { get; set; }
    public int Closed { get; set; }
    public int InProgress { get; set; }
    public int Landed { get; set; }
    public List<TypeDistItem> TypeDist { get; set; } = new();
    public List<MonthlyStatItem> MonthlyStat { get; set; } = new();
}

public class TypeDistItem
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public class MonthlyStatItem
{
    public int Month { get; set; }
    public int ClosedCount { get; set; }
    public int LandedCount { get; set; }
}
