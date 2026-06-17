using AssetManagement.Application.Common;

namespace AssetManagement.Application.Assets;

public interface IAssetService
{
    Task<PagedResult<AssetDto>> QueryAsync(AssetQuery query);
    Task<AssetDto> GetAsync(int id);
    Task<AssetDetailDto> GetDetailAsync(int id);
    Task<AssetDto> CreateAsync(CreateAssetRequest request);
    Task<AssetDto> UpdateAsync(int id, UpdateAssetRequest request);
    Task DeleteAsync(int id);
    Task<byte[]> ExportAsync(AssetQuery query);
    byte[] BuildImportTemplate();
    Task<List<ImportPreviewRow>> ValidateImportAsync(Stream file);
    Task<ImportConfirmResult> ConfirmImportAsync(Stream file);
}
