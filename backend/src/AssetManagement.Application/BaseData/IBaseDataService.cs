namespace AssetManagement.Application.BaseData;

public interface IBaseDataService
{
    Task<List<DepartmentNodeDto>> GetDepartmentTreeAsync();
    Task<List<OrganizationLevelDto>> GetOrganizationLevelsAsync();
    Task<DepartmentNodeDto> CreateDepartmentAsync(CreateDepartmentRequest request);
    Task<DepartmentNodeDto> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request);
    Task DeleteDepartmentAsync(int id);

    Task<List<CategoryNodeDto>> GetCategoryTreeAsync(string? deleteStatus = null);
    Task<CategoryNodeDto> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryNodeDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request);
    Task DeleteCategoryAsync(int id);
    Task PurgeCategoryAsync(int id);
    Task RestoreCategoryAsync(int id);

    Task<List<SystemSettingDto>> GetSettingsAsync();
    Task<RuntimeSettingsDto> GetRuntimeSettingsAsync();
    Task<List<SystemSettingDto>> SaveSettingsAsync(IEnumerable<SaveSystemSettingRequest> requests);
}
