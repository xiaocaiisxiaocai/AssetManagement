namespace AssetManagement.Application.TestMaterials;

public interface ITestProjectService
{
    Task<List<TestProjectDto>> ListAsync(string? deleteStatus);
    Task<TestProjectDto> CreateAsync(SaveTestProjectRequest request);
    Task<TestProjectDto> UpdateAsync(int id, SaveTestProjectRequest request);
    Task DeleteAsync(int id);
    Task RestoreAsync(int id);
    Task PurgeAsync(int id);
}
