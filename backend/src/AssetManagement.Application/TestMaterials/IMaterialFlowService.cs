using AssetManagement.Application.Common;

namespace AssetManagement.Application.TestMaterials;

public interface IMaterialFlowService
{
    /// <summary>发起转移:内部判断全局开关,关则进入待接收,开则生成审批单</summary>
    Task<MaterialFlowDto> InitiateTransferAsync(InitiateTransferRequest request, int applicantId);
    Task<List<TransfereeOptionDto>> GetTransfereeOptionsAsync(int materialId, string? keyword, int userId);
    Task<List<MaterialFlowDto>> PendingAsync(int userId, int? projectId = null);
    Task<PagedResult<MaterialFlowDto>> PendingPageAsync(int userId, MaterialFlowPageQuery query);
    Task<PagedResult<MaterialFlowDto>> HandledPageAsync(int userId, MaterialFlowPageQuery query);
    Task<List<MaterialFlowDto>> MineAsync(int userId, int? projectId = null);
    Task<PagedResult<MaterialFlowDto>> MinePageAsync(int userId, MaterialFlowPageQuery query);
    Task<PagedResult<MaterialFlowDto>> ReceivingPageAsync(int userId, MaterialFlowPageQuery query);
    Task<MaterialFlowDto> GetAsync(int id, int userId);
    Task<MaterialFlowDto> ApproveAsync(int id, MaterialApprovalRequest request, int userId);
    Task<MaterialFlowDto> RejectAsync(int id, MaterialRejectRequest request, int userId);
    Task<MaterialFlowDto> WithdrawAsync(int id, int userId);
    Task<MaterialFlowDto> ConfirmReceiptAsync(int id, int userId);
    Task<MaterialFlowDto> ReleaseStuckAsync(int id, int userId);
}
