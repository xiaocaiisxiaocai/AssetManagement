namespace AssetManagement.Application.BaseData;

public interface IBaseDataService
{
    Task<List<DepartmentNodeDto>> GetDepartmentTreeAsync();
    Task<DepartmentNodeDto> CreateDepartmentAsync(CreateDepartmentRequest request);
    Task<DepartmentNodeDto> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request);
    Task DeleteDepartmentAsync(int id);

    Task<List<CategoryNodeDto>> GetCategoryTreeAsync(string? deleteStatus = null);
    Task<CategoryNodeDto> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryNodeDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request);
    Task DeleteCategoryAsync(int id);
    Task PurgeCategoryAsync(int id);
    Task RestoreCategoryAsync(int id);

    Task<List<LocationNodeDto>> GetLocationTreeAsync();
    Task<LocationNodeDto> CreateLocationAsync(CreateLocationRequest request);
    Task<LocationNodeDto> UpdateLocationAsync(int id, UpdateLocationRequest request);
    Task DeleteLocationAsync(int id);

    Task<List<SystemSettingDto>> GetSettingsAsync();
    Task<List<SystemSettingDto>> SaveSettingsAsync(IEnumerable<SaveSystemSettingRequest> requests);
}
