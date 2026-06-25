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
    {
        var workflows = await _db.Workflows.OrderBy(x => x.Id).ToListAsync();
        return workflows.Select(x => ToWorkflowDto(x)).ToList();
    }

    public async Task<WorkflowDto> GetWorkflowAsync(int id)
        => ToWorkflowDto(await LoadWorkflow(id));

    public async Task<WorkflowDto> CreateWorkflowAsync(SaveWorkflowRequest request)
    {
        var workflow = new WorkflowEntity();
        await ApplyWorkflowDefinition(workflow, request);

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task<WorkflowDto> SaveWorkflowAsync(int id, SaveWorkflowRequest request)
    {
        var workflow = await LoadWorkflow(id);
        await ApplyWorkflowDefinition(workflow, request);

        await _db.SaveChangesAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task DeleteWorkflowAsync(int id)
    {
        var workflow = await LoadWorkflow(id);
        if (await _db.ApprovalFlows.AnyAsync(x => x.WorkflowId == id))
        {
            throw new BizException(4093, "已有审批实例使用该流程，不能删除");
        }

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();
    }

    public async Task<ApprovalFlowDto> StartAsync(StartApprovalRequest request, int applicantId)
    {
        var workflow = await _db.Workflows.SingleOrDefaultAsync(x => x.BizType == request.BizType)
            ?? throw new BizException(4049, "流程不存在");

        if (string.IsNullOrWhiteSpace(workflow.BpmnXml))
            throw new BizException(4051, "流程定义不完整，缺少 BPMN XML");

        var asset = await _db.Assets.FindAsync(request.AssetId)
            ?? throw new BizException(4048, "资产不存在");
        if (asset.IsDeleted)
        {
            throw new BizException(4048, "资产不存在");
        }

        // 发起前校验资产状态
        if (workflow.BizType is "borrow" or "transfer" && asset.Status != AssetStatus.Available)
        {
            throw new BizException(4055, "资产当前不可用,无法发起该流程");
        }

        // 防止并发发起
        if (await _db.ApprovalFlows.AnyAsync(x => x.AssetId == asset.Id && x.Status == "pending"))
        {
            throw new BizException(4056, "该资产已有进行中的审批,请勿重复发起");
        }

        var applicant = await _db.Users.FindAsync(applicantId)
            ?? throw new BizException(4041, "用户不存在");
        var transferee = request.TransfereeId.HasValue
            ? await _db.Users.FindAsync(request.TransfereeId.Value)
            : null;

        // 解析 BPMN 流程定义
        var bpmnProcess = BpmnParser.Parse(workflow.BpmnXml);

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
            Status = "pending",
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(2)
        };

        // 启动 BPMN 流程引擎
        BpmnEngine.Start(flow, bpmnProcess);

        _db.ApprovalFlows.Add(flow);
        await _db.SaveChangesAsync();
        await AddRecord(flow.Id, "start", applicant.Name, request.Reason);
        return ToFlowDto(flow);
    }

    public async Task<List<ApprovalFlowDto>> PendingAsync(int userId)
    {
        var user = await LoadUser(userId);
        var isAdmin = IsAdmin(user);
        var flows = await _db.ApprovalFlows
            .Where(x => x.Status == "pending")
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        // 筛选当前用户可以审批的流程
        var result = new List<ApprovalFlowDto>();
        foreach (var flow in flows)
        {
            if (isAdmin || await CanApprove(flow, user))
            {
                result.Add(ToFlowDto(flow));
            }
        }

        return result;
    }

    public async Task<List<ApprovalFlowDto>> MineAsync(int userId)
    {
        var flows = await _db.ApprovalFlows
            .Where(x => x.ApplicantId == userId)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return flows.Select(ToFlowDto).ToList();
    }

    public async Task<List<ApprovalFlowDto>> PendingReturnsAsync()
    {
        var flows = await _db.ApprovalFlows
            .Where(x => x.Status == "approved" && x.BizType == "borrow" && x.ConfirmedAt == null)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return flows.Select(ToFlowDto).ToList();
    }

    public async Task<ApprovalFlowDto> GetFlowAsync(int id)
        => ToFlowDto(await LoadFlow(id));

    public async Task<ApprovalFlowDto> ApproveAsync(int id, ApprovalActionRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);

        var user = await LoadUser(userId);

        // 确定审批的节点 ID
        var nodeId = request.NodeId;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            // 如果未指定节点，且只有一个活跃节点，则默认审批该节点
            if (flow.CurrentNodeIds.Count == 1)
            {
                nodeId = flow.CurrentNodeIds[0];
            }
            else
            {
                throw new BizException(4052, "存在多个待审批节点，请明确指定节点 ID");
            }
        }

        // 检查权限
        await EnsureCanApproveNode(flow, nodeId, user);

        // 获取 BPMN 流程定义
        var workflow = await LoadWorkflow(flow.WorkflowId);
        var bpmnProcess = BpmnParser.Parse(workflow.BpmnXml!);

        // 执行审批
        await using var tx = await _db.Database.BeginTransactionAsync();
        BpmnEngine.Approve(flow, bpmnProcess, nodeId, user.Name, request.Opinion);

        // 检查流程是否完成
        if (flow.Status == "approved")
        {
            await _bizEffectApplier.ApplyAsync(flow);
        }

        await _db.SaveChangesAsync();
        await AddRecord(id, "approve", user.Name, $"节点 {nodeId}: {request.Opinion}");
        await tx.CommitAsync();

        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> RejectAsync(int id, RejectRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);

        var user = await LoadUser(userId);

        // 确定驳回的节点 ID
        var nodeId = request.NodeId;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            if (flow.CurrentNodeIds.Count == 1)
            {
                nodeId = flow.CurrentNodeIds[0];
            }
            else
            {
                throw new BizException(4052, "存在多个待审批节点，请明确指定节点 ID");
            }
        }

        await EnsureCanApproveNode(flow, nodeId, user);

        await using var tx = await _db.Database.BeginTransactionAsync();
        BpmnEngine.Reject(flow, nodeId, user.Name, request.Reason);

        await _db.SaveChangesAsync();
        await AddRecord(id, "reject", user.Name, request.Reason);
        await tx.CommitAsync();

        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> AddSignAsync(int id, AddSignRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);

        var user = await LoadUser(userId);
        var nodeId = request.NodeId;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            if (flow.CurrentNodeIds.Count != 1)
                throw new BizException(4052, "存在多个待审批节点，请明确指定节点 ID");

            nodeId = flow.CurrentNodeIds[0];
        }

        await EnsureCanApproveNode(flow, nodeId, user);

        if (string.IsNullOrWhiteSpace(request.Who))
            throw new BizException(4057, "请选择加签人");

        var signUser = await _db.Users.SingleOrDefaultAsync(x => x.Name == request.Who || x.EmployeeNo == request.Who)
            ?? throw new BizException(4041, "加签人不存在");

        var token = flow.BpmnTokens.GetValueOrDefault(nodeId)
            ?? throw new BizException(4014, "该节点当前不可审批");

        token.SignStates ??= new Dictionary<string, bool>
        {
            [user.Name] = false
        };
        token.SignStates.TryAdd(signUser.Name, false);

        await _db.SaveChangesAsync();
        await AddRecord(id, "add_sign", user.Name, $"节点 {nodeId}: 加签 {signUser.Name}");
        return ToFlowDto(flow);
    }

    public async Task<ApprovalFlowDto> TransferSignAsync(int id, TransferSignRequest request, int userId)
    {
        // 转签功能暂不支持
        throw new BizException(4054, "BPMN 模式下暂不支持转签功能");
    }

    public async Task<ApprovalFlowDto> ConfirmReturnAsync(int id, int userId)
    {
        var flow = await LoadFlow(id);
        if (flow.Status != "approved" || flow.BizType != "borrow")
        {
            throw new BizException(4011, "该工单不可确认入库");
        }

        if (flow.ConfirmedAt.HasValue)
        {
            throw new BizException(4012, "该工单已确认入库");
        }

        var user = await LoadUser(userId);
        flow.ConfirmedAt = DateTime.UtcNow;

        var asset = await _db.Assets.FindAsync(flow.AssetId);
        if (asset != null)
        {
            asset.Status = AssetStatus.Available;
            asset.CustodianId = null;
        }

        await _db.SaveChangesAsync();
        await AddRecord(id, "confirm_return", user.Name, "确认归还入库");

        return ToFlowDto(flow);
    }

    // ========== 私有辅助方法 ==========

    private async Task<WorkflowEntity> LoadWorkflow(int id)
        => await _db.Workflows.FindAsync(id) ?? throw new BizException(4049, "流程不存在");

    private async Task ApplyWorkflowDefinition(WorkflowEntity workflow, SaveWorkflowRequest request)
    {
        var name = request.Name.Trim();
        var bizType = request.BizType.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BizException(4001, "流程名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(bizType))
        {
            throw new BizException(4001, "业务类型不能为空");
        }

        if (await _db.Workflows.AnyAsync(x => x.BizType == bizType && x.Id != workflow.Id))
        {
            throw new BizException(4094, "业务类型已存在");
        }

        ValidateBpmnXml(request.BpmnXml);

        workflow.Name = name;
        workflow.BizType = bizType;
        workflow.BpmnXml = request.BpmnXml;
    }

    private static void ValidateBpmnXml(string? bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml)) return;

        var securityErrors = BpmnValidator.ValidateSecurity(bpmnXml);
        if (securityErrors.Any())
        {
            throw new BizException(4051, $"BPMN 安全验证失败: {string.Join("; ", securityErrors)}");
        }

        var structureErrors = BpmnValidator.Validate(bpmnXml);
        if (structureErrors.Any())
        {
            throw new BizException(4050, $"BPMN 结构验证失败: {string.Join("; ", structureErrors)}");
        }

        var process = BpmnParser.Parse(bpmnXml);
        var parseErrors = BpmnParser.Validate(process);
        if (parseErrors.Any())
        {
            throw new BizException(4050, $"BPMN 解析验证失败: {string.Join("; ", parseErrors)}");
        }
    }

    private async Task<ApprovalFlow> LoadFlow(int id)
        => await _db.ApprovalFlows.FindAsync(id) ?? throw new BizException(4010, "审批工单不存在");

    private async Task<User> LoadUser(int id)
        => await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).SingleOrDefaultAsync(u => u.Id == id)
            ?? throw new BizException(4041, "用户不存在");

    private void EnsureActive(ApprovalFlow flow)
    {
        if (flow.Status != "pending")
        {
            throw new BizException(4013, "该工单已结束，无法操作");
        }
    }

    private async Task<bool> CanApprove(ApprovalFlow flow, User user)
    {
        // 检查用户是否可以审批流程中的任一活跃节点
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (flow.BpmnTokens.TryGetValue(nodeId, out var token) && token.Status == BpmnTokenStatus.Active)
            {
                var workflow = await _db.Workflows.FindAsync(flow.WorkflowId);
                if (workflow?.BpmnXml == null) continue;

                var bpmnProcess = BpmnParser.Parse(workflow.BpmnXml);
                var node = bpmnProcess.FindNode(nodeId);
                if (node?.Type == BpmnNodeType.UserTask)
                {
                    if (await IsApproverForNode(node, user, flow))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private async Task EnsureCanApproveNode(ApprovalFlow flow, string nodeId, User user)
    {
        if (!flow.CurrentNodeIds.Contains(nodeId))
        {
            throw new BizException(4014, "该节点当前不可审批");
        }

        var workflow = await LoadWorkflow(flow.WorkflowId);
        var bpmnProcess = BpmnParser.Parse(workflow.BpmnXml!);
        var node = bpmnProcess.FindNode(nodeId);

        if (node == null || node.Type != BpmnNodeType.UserTask)
        {
            throw new BizException(4015, "无效的审批节点");
        }

        if (IsAdmin(user))
        {
            return;
        }

        if (!await IsApproverForNode(node, user, flow))
        {
            throw new BizException(4016, "您无权审批此节点");
        }
    }

    private async Task<bool> IsApproverForNode(BpmnNode node, User user, ApprovalFlow flow)
    {
        if (flow.BpmnTokens.TryGetValue(node.Id, out var token) &&
            token.SignStates is { Count: > 0 } &&
            token.SignStates.TryGetValue(user.Name, out var signed))
        {
            return !signed;
        }

        // 从节点属性中获取审批人配置
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        // 指定用户
        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "deptManager")
            {
                var applicant = await _db.Users.FindAsync(flow.ApplicantId);
                if (applicant?.DepartmentId is null)
                {
                    return false;
                }

                var department = await _db.Departments.FindAsync(applicant.DepartmentId.Value);
                var isSameDeptAdmin = user.DepartmentId == applicant.DepartmentId &&
                                      user.UserRoles.Any(ur => ur.Role?.Code == "dept_admin");
                var isDepartmentManager = department?.ManagerId == user.Id;
                return isSameDeptAdmin || isDepartmentManager;
            }
            else if (assignee == "supervisor")
            {
                var applicant = await _db.Users.FindAsync(flow.ApplicantId);
                return applicant?.SupervisorId == user.Id;
            }
            else if (int.TryParse(assignee, out var userId))
            {
                return user.Id == userId;
            }
            else
            {
                return user.Name == assignee || user.EmployeeNo == assignee;
            }
        }

        // 候选用户列表
        if (!string.IsNullOrEmpty(candidateUsers))
        {
            var users = candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (users.Any(u => u.Trim() == user.Id.ToString() || u.Trim() == user.Name))
            {
                return true;
            }
        }

        // 候选角色
        if (!string.IsNullOrEmpty(candidateGroups))
        {
            var groups = candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (user.UserRoles.Any(ur =>
                    ur.Role != null &&
                    groups.Any(group => group.Trim() == ur.Role.Code || group.Trim() == ur.Role.Name)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAdmin(User user)
        => user.UserRoles.Any(ur => ur.Role?.Code == "admin");

    private async Task<string?> DepartmentName(int? deptId)
    {
        if (!deptId.HasValue) return null;
        var dept = await _db.Departments.FindAsync(deptId.Value);
        return dept?.Name;
    }

    private async Task AddRecord(int flowId, string action, string actor, string? remark)
    {
        _db.FlowRecords.Add(new FlowRecord
        {
            FlowId = flowId,
            Action = action,
            Operator = actor,
            Comment = remark,
            OperatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static WorkflowDto ToWorkflowDto(WorkflowEntity w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        BizType = w.BizType,
        BpmnXml = w.BpmnXml
    };

    private static ApprovalFlowDto ToFlowDto(ApprovalFlow f) => new()
    {
        Id = f.Id,
        FlowNo = f.FlowNo,
        BizType = f.BizType,
        AssetId = f.AssetId,
        AssetNo = f.AssetNo,
        AssetName = f.AssetName,
        Applicant = f.Applicant,
        ApplicantDept = f.ApplicantDept,
        Transferee = f.Transferee,
        TransfereeDept = f.TransfereeDept,
        Reason = f.Reason,
        ReturnDate = f.ReturnDate,
        Status = f.Status,
        CurrentNodeIds = f.CurrentNodeIds,
        BpmnTokens = f.BpmnTokens,
        ApplyTime = f.ApplyTime,
        Deadline = f.Deadline,
        ConfirmedAt = f.ConfirmedAt
    };
}
