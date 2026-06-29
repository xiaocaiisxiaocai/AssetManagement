using AssetManagement.Application.Common;

namespace AssetManagement.Application.TestMaterials;

public interface ITestMaterialService
{
    Task<PagedResult<TestMaterialDto>> QueryAsync(TestMaterialQuery query);
    Task<TestMaterialDto> GetAsync(int id);
    Task<TestMaterialDetailDto> GetDetailAsync(int id);
    Task<TestMaterialDto> CreateAsync(SaveTestMaterialRequest request);
    Task<TestMaterialDto> UpdateAsync(int id, SaveTestMaterialRequest request);
    Task DeleteAsync(int id);
    Task RestoreAsync(int id);
    Task PurgeAsync(int id);
    /// <summary>退回厂商:状态置为 ReturnedToVendor</summary>
    Task<TestMaterialDto> ReturnToVendorAsync(int id);
}
