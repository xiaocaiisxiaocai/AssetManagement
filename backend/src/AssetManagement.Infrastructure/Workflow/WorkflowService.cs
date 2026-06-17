using AssetManagement.Application.Common;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Infrastructure.Workflow;

public class WorkflowService : IWorkflowService
{
    private readonly AppDbContext _db;
    private readonly IBizEffectApplier _bizEffectApplier;

    public WorkflowService(AppDbContext db, IBizEffectApplier bizEffectApplier)
    {
        _db = db;
        _bizEffectApplier = bizEffectApplier;
    }

    public async Task<List<WorkflowDto>> GetWorkflowsAsync()
        => await _db.Workflows.OrderBy(x => x.Id).Select(x => ToWorkflowDto(x)).ToListAsync();

    public async Task<WorkflowDto> GetWorkflowAsync(int id)
        => ToWorkflowDto(await LoadWorkflow(id));

    public async Task<WorkflowDto> SaveWorkflowAsync(int id, SaveWorkflowRequest request)
    {
        ValidateNodes(request.Nodes);
        var workflow = await LoadWorkflow(id);
        workflow.Name = request.Name.Trim();
        workflow.BizType = request.BizType.Trim();
        workflow.Nodes = request.Nodes.ToList();
        await _db.SaveChangesAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task<ApprovalFlowDto> StartAsync(StartApprovalRequest request, int applicantId)
    {
        var workflow = await _db.Workflows.SingleOrDefaultAsync(x => x.BizType == request.BizType)
            ?? throw new BizException(4049, "流程不存在");
        var asset = await _db.Assets.FindAsync(request.AssetId)
            ?? throw new BizException(4048, "资产不存在");
        var applicant = await _db.Users.FindAsync(applicantId)
            ?? throw new BizException(4041, "用户不存在");
        var transferee = request.TransfereeId.HasValue
            ? await _db.Users.FindAsync(request.TransfereeId.Value)
            : null;

        var flow = new ApprovalFlow
        {
            FlowNo = $"APV-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            BizType = workflow.BizType,
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            ApplicantDept = await DepartmentName(applicant.DepartmentId),
            TransfereeId = transferee?.Id,
            Transferee = transferee?.Name,
            TransfereeDept = await DepartmentName(transferee?.DepartmentId),
            Reason = request.Reason,
            ReturnDate = workflow.BizType == "borrow" ? request.ReturnDate : null,
            Amount = asset.Price,
            Status = "pending",
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(2),
            Nodes = workflow.Nodes.Select(ToInstanceNode).ToList()
        };
        WorkflowEngine.Start(flow);
        _db.ApprovalFlows.Add(flow);
        await _db.SaveChangesAsync();
        await AddRecord(flow.Id, "start", applicant.Name, request.Reason);
        return ToFlowDto(flow);
    }

    public async Task<List<ApprovalFlowDto>> PendingAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        var flows = await _db.ApprovalFlows.Where(x => x.Status == "pending").OrderByDescending(x => x.Id).ToListAsync();
        return flows
            .Where(x => IsPendingForUser(x, user))
            .Select(ToFlowDto)
            .ToList();
    }

    public async Task<List<ApprovalFlowDto>> MineAsync(int userId)
        => await _db.ApprovalFlows
            .Where(x => x.ApplicantId == userId)
            .OrderByDescending(x => x.Id)
            .Select(x => ToFlowDto(x))
            .ToListAsync();

    // 待入库:全局已审批通过、尚未确认入库的借用流程单(由资产管理员处理,不限发起人)
    public async Task<List<ApprovalFlowDto>> PendingReturnsAsync()
        => await _db.ApprovalFlows
            .Where(x => x.Status == "approved" && x.BizType == "borrow" && x.ConfirmedAt == null)
            .OrderByDescending(x => x.Id)
            .Select(x => ToFlowDto(x))
            .ToListAsync();

    public async Task<ApprovalFlowDto> GetFlowAsync(int id)
        => ToFlowDto(await LoadFlow(id));

    public async Task<ApprovalFlowDto> ApproveAsync(int id, ApprovalActionRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        WorkflowEngine.Approve(flow, request.Signer, request.Opinion);
        flow.Nodes = flow.Nodes.ToList();
        if (WorkflowEngine.IsFinished(flow))
        {
            await _bizEffectApplier.ApplyAsync(flow);
        }

        await _db.SaveChangesAsync();
        await AddRecord(id, "approve", await UserName(userId), request.Opinion);
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> RejectAsync(int id, RejectRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        WorkflowEngine.Reject(flow, request.Reason);
        flow.Nodes = flow.Nodes.ToList();
        await _db.SaveChangesAsync();
        await AddRecord(id, "reject", await UserName(userId), request.Reason);
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> AddSignAsync(int id, AddSignRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        WorkflowEngine.AddSign(flow, request.Who);
        flow.Nodes = flow.Nodes.ToList();
        await _db.SaveChangesAsync();
        await AddRecord(id, "add-sign", await UserName(userId), request.Who);
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> TransferSignAsync(int id, TransferSignRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        WorkflowEngine.Transfer(flow, request.Who);
        flow.Nodes = flow.Nodes.ToList();
        await _db.SaveChangesAsync();
        await AddRecord(id, "transfer-sign", await UserName(userId), request.Who);
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> ConfirmReturnAsync(int id)
    {
        var flow = await LoadFlow(id)
            ?? throw new BizException(4050, "审批工单不存在");

        if (flow.Status != "approved")
        {
            throw new BizException(4051, "只能确认已通过的归还申请");
        }

        if (flow.BizType != "borrow")
        {
            throw new BizException(4052, "只有借用申请可以确认入库");
        }

        var asset = await _db.Assets.FindAsync(flow.AssetId)
            ?? throw new BizException(4048, "资产不存在");

        asset.Status = 0;
        asset.CustodianId = null;
        _db.Assets.Update(asset);

        flow.ConfirmedAt = DateTime.UtcNow;
        _db.ApprovalFlows.Update(flow);

        await _db.SaveChangesAsync();
        await AddRecord(id, "confirm-return", "System", "Confirmed asset return to inventory");

        return ToFlowDto(flow);
    }

    private async Task<WorkflowEntity> LoadWorkflow(int id)
        => await _db.Workflows.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4049, "流程不存在");

    private async Task<ApprovalFlow> LoadFlow(int id)
        => await _db.ApprovalFlows.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BizException(4050, "审批工单不存在");

    private async Task AddRecord(int flowId, string action, string? operatorName, string? comment)
    {
        _db.FlowRecords.Add(new FlowRecord
        {
            FlowId = flowId,
            Action = action,
            Operator = operatorName,
            Comment = comment,
            OperatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task<string> UserName(int userId)
        => (await _db.Users.FindAsync(userId))?.Name ?? "";

    private async Task<string?> DepartmentName(int? id)
        => id.HasValue ? (await _db.Departments.FindAsync(id.Value))?.Name : null;

    private static FlowInstanceNode ToInstanceNode(WorkflowNode node) => new()
    {
        Name = node.Name,
        Type = node.Type,
        Approver = node.Approver,
        Signers = node.Signers?.ToList(),
        SignStates = node.Signers?.ToDictionary(x => x, _ => false),
        Condition = node.Condition,
        Status = node.Type == NodeType.Start ? NodeStatus.Done : NodeStatus.Pending
    };

    private static bool IsPendingForUser(ApprovalFlow flow, User? user)
    {
        var node = flow.Nodes.ElementAtOrDefault(flow.CurrentNodeIndex);
        if (node is null || user is null)
        {
            return false;
        }

        return node.Approver == user.Name
            || node.Signers?.Contains(user.Name) == true
            || node.AddedSigners?.Contains(user.Name) == true
            || user.EmployeeNo == "1001";
    }

    private static void ValidateNodes(List<WorkflowNode> nodes)
    {
        if (nodes.Count < 2 || nodes.First().Type != NodeType.Start || nodes.Last().Type != NodeType.End)
        {
            throw new BizException(4002, "流程必须以发起开始、以结束收尾");
        }

        if (nodes.Any(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            throw new BizException(4003, "节点名称不能为空");
        }
    }

    private static WorkflowDto ToWorkflowDto(WorkflowEntity x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        BizType = x.BizType,
        Nodes = x.Nodes
    };

    private static ApprovalFlowDto ToFlowDto(ApprovalFlow x) => new()
    {
        Id = x.Id,
        FlowNo = x.FlowNo,
        BizType = x.BizType,
        AssetId = x.AssetId,
        AssetNo = x.AssetNo,
        AssetName = x.AssetName,
        Applicant = x.Applicant,
        ApplicantDept = x.ApplicantDept,
        Transferee = x.Transferee,
        TransfereeDept = x.TransfereeDept,
        Reason = x.Reason,
        ReturnDate = x.ReturnDate,
        Amount = x.Amount,
        Status = x.Status,
        CurrentNodeIndex = x.CurrentNodeIndex,
        Nodes = x.Nodes,
        ApplyTime = x.ApplyTime,
        Deadline = x.Deadline,
        ConfirmedAt = x.ConfirmedAt
    };
}
