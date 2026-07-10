namespace AssetManagement.Application.Workflow;

public interface IWorkflowService
{
    Task<List<WorkflowDto>> GetWorkflowsAsync();
    Task<WorkflowDto> GetWorkflowAsync(int id);
    Task<WorkflowDto> CreateWorkflowAsync(SaveWorkflowRequest request);
    Task<WorkflowDto> SaveWorkflowAsync(int id, SaveWorkflowRequest request);
    Task<WorkflowDto> SetWorkflowStatusAsync(int id, bool isActive);
    Task DeleteWorkflowAsync(int id);
    Task<ApprovalFlowDto> StartAsync(StartApprovalRequest request, int applicantId);
    Task<List<ApprovalFlowDto>> PendingAsync(int userId);
    Task<List<ApprovalFlowDto>> PendingReturnsAsync(int userId);
    Task<List<ApprovalFlowDto>> MineAsync(int userId);
    Task<ApprovalFlowDto> GetFlowAsync(int id, int userId);
    Task<ApprovalFlowDto> ApproveAsync(int id, ApprovalActionRequest request, int userId);
    Task<ApprovalFlowDto> RejectAsync(int id, RejectRequest request, int userId);
    Task<ApprovalFlowDto> AddSignAsync(int id, AddSignRequest request, int userId);
    Task<ApprovalFlowDto> TransferSignAsync(int id, TransferSignRequest request, int userId);
    Task<ApprovalFlowDto> ConfirmReturnAsync(int id, int userId);
}

public interface IBizEffectApplier
{
    Task ApplyAsync(AssetManagement.Domain.Entities.ApprovalFlow flow, int? operatorUserId = null);
}
