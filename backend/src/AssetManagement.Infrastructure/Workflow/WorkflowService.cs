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
        // 发起前校验资产状态,避免对借出中/不可用资产重复发起借用或转让
        if (workflow.BizType is "borrow" or "transfer" && asset.Status != AssetStatus.Available)
        {
            throw new BizException(4055, "资产当前不可用,无法发起该流程");
        }
        // 同一资产存在进行中的审批时拒绝重复发起,防止并发借用竞态
        if (await _db.ApprovalFlows.AnyAsync(x => x.AssetId == asset.Id && x.Status == "pending"))
        {
            throw new BizException(4056, "该资产已有进行中的审批,请勿重复发起");
        }
        var applicant = await _db.Users.FindAsync(applicantId)
            ?? throw new BizException(4041, "用户不存在");
        var transferee = request.TransfereeId.HasValue
            ? await _db.Users.FindAsync(request.TransfereeId.Value)
            : null;

        // 解析每个节点的实际审批人(直属上级/部门经理等动态类型)
        var instanceNodes = new List<FlowInstanceNode>();
        foreach (var node in workflow.Nodes)
        {
            instanceNodes.Add(ToInstanceNode(node, await ResolveApproverAsync(node, applicant)));
        }

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
            Nodes = instanceNodes
        };
        WorkflowEngine.Start(flow);
        _db.ApprovalFlows.Add(flow);
        await _db.SaveChangesAsync();
        await AddRecord(flow.Id, "start", applicant.Name, request.Reason);
        return ToFlowDto(flow);
    }

    public async Task<List<ApprovalFlowDto>> PendingAsync(int userId)
    {
        var user = await LoadUser(userId);
        var isAdmin = IsAdmin(user);
        var flows = await _db.ApprovalFlows.Where(x => x.Status == "pending").OrderByDescending(x => x.Id).ToListAsync();
        return flows
            .Where(x => isAdmin || IsNodeApprover(CurrentNodeOrNull(x), user))
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
        EnsureActive(flow);
        var user = await LoadUser(userId);
        EnsureCanHandle(flow, user);

        // 流程推进、业务副作用、流转记录三者同一事务,任一失败整体回滚,避免状态不一致
        await using var tx = await _db.Database.BeginTransactionAsync();
        // 会签/或签未显式指定签署人时,默认以当前审批人身份签署(EnsureCanHandle 已确保其为合法审批人)
        WorkflowEngine.Approve(flow, request.Signer ?? user.Name, request.Opinion);
        flow.Nodes = flow.Nodes.ToList();
        if (WorkflowEngine.IsFinished(flow))
        {
            await _bizEffectApplier.ApplyAsync(flow);
        }

        await _db.SaveChangesAsync();
        await AddRecord(id, "approve", user.Name, request.Opinion);
        await tx.CommitAsync();
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> RejectAsync(int id, RejectRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        var user = await LoadUser(userId);
        EnsureCanHandle(flow, user);
        WorkflowEngine.Reject(flow, request.Reason);
        flow.Nodes = flow.Nodes.ToList();
        await _db.SaveChangesAsync();
        await AddRecord(id, "reject", user.Name, request.Reason);
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> AddSignAsync(int id, AddSignRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        var user = await LoadUser(userId);
        EnsureCanHandle(flow, user);
        WorkflowEngine.AddSign(flow, request.Who);
        flow.Nodes = flow.Nodes.ToList();
        await _db.SaveChangesAsync();
        await AddRecord(id, "add-sign", user.Name, request.Who);
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> TransferSignAsync(int id, TransferSignRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        var user = await LoadUser(userId);
        EnsureCanHandle(flow, user);
        WorkflowEngine.Transfer(flow, request.Who);
        flow.Nodes = flow.Nodes.ToList();
        await _db.SaveChangesAsync();
        await AddRecord(id, "transfer-sign", user.Name, request.Who);
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

    private async Task<string?> DepartmentName(int? id)
        => id.HasValue ? (await _db.Departments.FindAsync(id.Value))?.Name : null;

    private async Task<User> LoadUser(int userId)
        => await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new BizException(4041, "用户不存在");

    private static bool IsAdmin(User user)
        => user.UserRoles.Any(ur => ur.Role.IsActive && ur.Role.Code == "admin");

    private static FlowInstanceNode? CurrentNodeOrNull(ApprovalFlow flow)
        => flow.Nodes.ElementAtOrDefault(flow.CurrentNodeIndex);

    private static bool IsNodeApprover(FlowInstanceNode? node, User user)
        => node is not null
           && (node.Approver == user.Name
               || node.Signers?.Contains(user.Name) == true
               || node.AddedSigners?.Contains(user.Name) == true);

    // 超级管理员(admin 角色)可处理任意工单;其余必须是当前节点的指定审批人/会签人
    private static void EnsureCanHandle(ApprovalFlow flow, User user)
    {
        if (IsAdmin(user))
        {
            return;
        }
        if (!IsNodeApprover(CurrentNodeOrNull(flow), user))
        {
            throw new BizException(4053, "无权处理该审批工单");
        }
    }

    // 终态保护:已通过/已驳回的流程不允许再次推进或驳回
    private static void EnsureActive(ApprovalFlow flow)
    {
        if (flow.Status is "approved" or "rejected")
        {
            throw new BizException(4054, "审批流程已结束,无法继续操作");
        }
    }

    private static FlowInstanceNode ToInstanceNode(WorkflowNode node, string? resolvedApprover) => new()
    {
        Name = node.Name,
        Type = node.Type,
        Approver = resolvedApprover,
        Signers = node.Signers?.ToList(),
        SignStates = node.Signers?.ToDictionary(x => x, _ => false),
        Condition = node.Condition,
        Status = node.Type == NodeType.Start ? NodeStatus.Done : NodeStatus.Pending
    };

    // 按审批人类型解析实际审批人姓名;动态类型解析不到时回退节点配置值,避免流程卡死
    private async Task<string?> ResolveApproverAsync(WorkflowNode node, User applicant)
        => node.ApproverType switch
        {
            ApproverType.Supervisor => await UserNameById(applicant.SupervisorId) ?? node.Approver,
            ApproverType.DeptManager => await DeptManagerName(applicant.DepartmentId) ?? node.Approver,
            _ => node.Approver
        };

    private async Task<string?> UserNameById(int? userId)
        => userId.HasValue ? (await _db.Users.FindAsync(userId.Value))?.Name : null;

    private async Task<string?> DeptManagerName(int? deptId)
    {
        if (!deptId.HasValue)
        {
            return null;
        }
        var dept = await _db.Departments.FindAsync(deptId.Value);
        return dept?.ManagerId is { } managerId ? await UserNameById(managerId) : null;
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
