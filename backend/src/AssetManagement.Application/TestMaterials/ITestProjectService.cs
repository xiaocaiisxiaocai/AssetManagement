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
}
