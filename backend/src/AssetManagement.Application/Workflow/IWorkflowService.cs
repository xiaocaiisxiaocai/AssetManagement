using AssetManagement.Application.Common;

namespace AssetManagement.Application.Workflow;

public interface IWorkflowService
{
    Task<List<WorkflowDto>> GetWorkflowsAsync();
    Task<WorkflowDto> GetWorkflowAsync(int id);
    Task<WorkflowDto> CreateWorkflowAsync(SaveWorkflowRequest request);
    Task<WorkflowDto> SaveWorkflowAsync(int id, UpdateWorkflowMetadataRequest request);
    Task<WorkflowDto> SaveWorkflowDesignAsync(int id, DesignWorkflowRequest request);
    Task<WorkflowDto> SetWorkflowStatusAsync(int id, bool isActive);
    Task DeleteWorkflowAsync(int id);
    Task<ApprovalFlowDto> StartAsync(StartApprovalRequest request, int applicantId);
    Task<List<ApprovalFlowDto>> PendingAsync(int userId);
    Task<PagedResult<ApprovalFlowDto>> PendingPageAsync(int userId, ApprovalFlowPageQuery query);
    Task<PagedResult<ApprovalFlowDto>> HandledPageAsync(int userId, ApprovalFlowPageQuery query);
    Task<List<ApprovalFlowDto>> PendingReturnsAsync(int userId);
    Task<PagedResult<ApprovalFlowDto>> PendingReturnsPageAsync(int userId, ApprovalFlowPageQuery query);
    Task<List<ApprovalFlowDto>> MineAsync(int userId);
    Task<PagedResult<ApprovalFlowDto>> MinePageAsync(int userId, ApprovalFlowPageQuery query);
    Task<ApprovalFlowDto> GetFlowAsync(int id, int userId);
    Task<ApprovalFlowDto> ApproveAsync(int id, ApprovalActionRequest request, int userId);
    Task<ApprovalFlowDto> RejectAsync(int id, RejectRequest request, int userId);
    Task<ApprovalFlowDto> WithdrawAsync(int id, int userId);
    Task<ApprovalFlowDto> AddSignAsync(int id, AddSignRequest request, int userId);
    Task<ApprovalFlowDto> CancelAddSignAsync(int id, CancelAddSignRequest request, int userId);
    Task<ApprovalFlowDto> TransferSignAsync(int id, TransferSignRequest request, int userId);
    Task<ApprovalFlowDto> ConfirmReturnAsync(int id, int userId);
}

public interface IBizEffectApplier
{
    Task ApplyAsync(AssetManagement.Domain.Entities.ApprovalFlow flow, int? operatorUserId = null);
}
