using AssetManagement.Application.Common;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Infrastructure.TestMaterials;

public class MaterialFlowService : IMaterialFlowService
{
    private readonly AppDbContext _db;
    public const string ApprovalSwitchKey = "material.transfer.approval.enabled";
    public const string MaterialBizType = "material_transfer";

    public MaterialFlowService(AppDbContext db) => _db = db;

    public async Task<MaterialFlowDto> InitiateTransferAsync(InitiateTransferRequest request, int applicantId)
    {
        var material = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == request.MaterialId)
            ?? throw new BizException(4048, "测试料件不存在");
        if (material.IsDeleted) throw new BizException(4048, "测试料件不存在");

        if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == material.Id && x.Status == "pending"))
            throw new BizException(4056, "该料件已有进行中的流转,请勿重复发起");

        var applicant = await _db.Users.FindAsync(applicantId) ?? throw new BizException(4041, "用户不存在");
        var transferee = await _db.Users.FindAsync(request.TransfereeId)
            ?? throw new BizException(4041, "受让人不存在");

        var approvalEnabled = await IsApprovalEnabled();

        // 开关关闭:直接转移(仍落一条 status=approved 的流转单,保证详情时间线可追溯)
        if (!approvalEnabled)
        {
            material.CustodianId = transferee.Id;
            var directFlow = new MaterialFlow
            {
                FlowNo = $"MTF-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                BizType = MaterialBizType,
                WorkflowId = 0,
                MaterialId = material.Id,
                MaterialNo = material.MaterialNo,
                MaterialName = material.Name,
                ApplicantId = applicant.Id,
                Applicant = applicant.Name,
                ApplicantDept = await DepartmentName(applicant.DepartmentId),
                TransfereeId = transferee.Id,
                Transferee = transferee.Name,
                TransfereeDept = await DepartmentName(transferee.DepartmentId),
                Reason = request.Reason,
                Status = "approved",
                ApplyTime = DateTime.UtcNow,
                Deadline = DateTime.UtcNow
            };
            _db.MaterialFlows.Add(directFlow);
            await _db.SaveChangesAsync();
            await AddRecord(directFlow.Id, "direct_transfer", applicant.Name,
                $"直接转移给 {transferee.Name}: {request.Reason}");
            var dto = ToDto(directFlow);
            dto.DirectTransfer = true;
            return dto;
        }

        // 开关开启:走 BPMN 审批
        var workflow = await _db.Workflows.SingleOrDefaultAsync(x => x.BizType == MaterialBizType)
            ?? throw new BizException(4049, "测试料件流转流程未配置");
        if (string.IsNullOrWhiteSpace(workflow.BpmnXml))
            throw new BizException(4051, "流程定义不完整,缺少 BPMN XML");

        var process = BpmnParser.Parse(workflow.BpmnXml);
        var flow = new MaterialFlow
        {
            FlowNo = $"MTF-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            BizType = MaterialBizType,
            WorkflowId = workflow.Id,
            MaterialId = material.Id,
            MaterialNo = material.MaterialNo,
            MaterialName = material.Name,
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            ApplicantDept = await DepartmentName(applicant.DepartmentId),
            TransfereeId = transferee.Id,
            Transferee = transferee.Name,
            TransfereeDept = await DepartmentName(transferee.DepartmentId),
            Reason = request.Reason,
            Status = "pending",
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(2)
        };
        BpmnEngine.Start(flow, process);
        _db.MaterialFlows.Add(flow);
        await _db.SaveChangesAsync();
        await AddRecord(flow.Id, "start", applicant.Name, request.Reason);
        return ToDto(flow);
    }

    public async Task<List<MaterialFlowDto>> PendingAsync(int userId)
    {
        var user = await LoadUser(userId);
        var isAdmin = user.UserRoles.Any(ur => ur.Role?.Code == "admin");
        var flows = await _db.MaterialFlows.Where(x => x.Status == "pending")
            .OrderByDescending(x => x.Id).ToListAsync();
        var result = new List<MaterialFlowDto>();
        foreach (var flow in flows)
        {
            if (isAdmin || await CanApprove(flow, user)) result.Add(ToDto(flow));
        }
        return result;
    }

    public async Task<List<MaterialFlowDto>> MineAsync(int userId)
    {
        var flows = await _db.MaterialFlows.Where(x => x.ApplicantId == userId)
            .OrderByDescending(x => x.Id).ToListAsync();
        return flows.Select(ToDto).ToList();
    }

    public async Task<MaterialFlowDto> GetAsync(int id) => ToDto(await LoadFlow(id));

    public async Task<MaterialFlowDto> ApproveAsync(int id, MaterialApprovalRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        var user = await LoadUser(userId);
        var nodeId = ResolveNode(flow, request.NodeId);
        await EnsureCanApproveNode(flow, nodeId, user);

        var workflow = await LoadWorkflow(flow.WorkflowId);
        var process = BpmnParser.Parse(workflow.BpmnXml!);

        await using var tx = await _db.Database.BeginTransactionAsync();
        BpmnEngine.Approve(flow, process, nodeId, user.Name, request.Opinion);

        // 流程完成 -> 落地业务副作用(改保管人)
        if (flow.Status == "approved")
        {
            var material = await _db.TestMaterials.FindAsync(flow.MaterialId);
            if (material != null && flow.TransfereeId.HasValue)
                material.CustodianId = flow.TransfereeId.Value;
        }

        await _db.SaveChangesAsync();
        await AddRecord(id, "approve", user.Name, $"节点 {nodeId}: {request.Opinion}");
        await tx.CommitAsync();
        return ToDto(flow);
    }

    public async Task<MaterialFlowDto> RejectAsync(int id, MaterialRejectRequest request, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        var user = await LoadUser(userId);
        var nodeId = ResolveNode(flow, request.NodeId);
        await EnsureCanApproveNode(flow, nodeId, user);

        await using var tx = await _db.Database.BeginTransactionAsync();
        BpmnEngine.Reject(flow, nodeId, user.Name, request.Reason);
        await _db.SaveChangesAsync();
        await AddRecord(id, "reject", user.Name, request.Reason);
        await tx.CommitAsync();
        return ToDto(flow);
    }

    // ===== 私有辅助 =====
    private async Task<bool> IsApprovalEnabled()
    {
        var setting = await _db.SystemSettings.SingleOrDefaultAsync(x => x.Key == ApprovalSwitchKey);
        return setting != null && string.Equals(setting.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<MaterialFlow> LoadFlow(int id)
        => await _db.MaterialFlows.FindAsync(id) ?? throw new BizException(4010, "流转单不存在");

    private async Task<WorkflowEntity> LoadWorkflow(int id)
        => await _db.Workflows.FindAsync(id) ?? throw new BizException(4049, "流程不存在");

    private async Task<User> LoadUser(int id)
        => await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).SingleOrDefaultAsync(u => u.Id == id)
            ?? throw new BizException(4041, "用户不存在");

    private static void EnsureActive(MaterialFlow flow)
    {
        if (flow.Status != "pending") throw new BizException(4013, "该流转单已结束,无法操作");
    }

    private static string ResolveNode(MaterialFlow flow, string? nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId)) return nodeId;
        if (flow.CurrentNodeIds.Count == 1) return flow.CurrentNodeIds[0];
        throw new BizException(4052, "存在多个待审批节点,请明确指定节点 ID");
    }

    private async Task<bool> CanApprove(MaterialFlow flow, User user)
    {
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (flow.BpmnTokens.TryGetValue(nodeId, out var token) && token.Status == BpmnTokenStatus.Active)
            {
                var workflow = await _db.Workflows.FindAsync(flow.WorkflowId);
                if (workflow?.BpmnXml == null) continue;
                var process = BpmnParser.Parse(workflow.BpmnXml);
                var node = process.FindNode(nodeId);
                if (node?.Type == BpmnNodeType.UserTask && await IsApproverForNode(node, user, flow)) return true;
            }
        }
        return false;
    }

    private async Task EnsureCanApproveNode(MaterialFlow flow, string nodeId, User user)
    {
        if (!flow.CurrentNodeIds.Contains(nodeId)) throw new BizException(4014, "该节点当前不可审批");
        var workflow = await LoadWorkflow(flow.WorkflowId);
        var process = BpmnParser.Parse(workflow.BpmnXml!);
        var node = process.FindNode(nodeId);
        if (node == null || node.Type != BpmnNodeType.UserTask) throw new BizException(4015, "无效的审批节点");
        if (user.UserRoles.Any(ur => ur.Role?.Code == "admin")) return;
        if (!await IsApproverForNode(node, user, flow)) throw new BizException(4016, "您无权审批此节点");
    }

    private async Task<bool> IsApproverForNode(BpmnNode node, User user, MaterialFlow flow)
    {
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "deptManager")
            {
                var applicant = await _db.Users.FindAsync(flow.ApplicantId);
                if (applicant?.DepartmentId is null) return false;
                var department = await _db.Departments.FindAsync(applicant.DepartmentId.Value);
                var isSameDeptAdmin = user.DepartmentId == applicant.DepartmentId &&
                                      user.UserRoles.Any(ur => ur.Role?.Code == "dept_admin");
                var isDepartmentManager = department?.ManagerId == user.Id;
                return isSameDeptAdmin || isDepartmentManager;
            }
            if (assignee == "supervisor")
            {
                var applicant = await _db.Users.FindAsync(flow.ApplicantId);
                return applicant?.SupervisorId == user.Id;
            }
            if (int.TryParse(assignee, out var uid)) return user.Id == uid;
            return user.Name == assignee || user.EmployeeNo == assignee;
        }
        if (!string.IsNullOrEmpty(candidateUsers))
        {
            var users = candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (users.Any(u => u.Trim() == user.Id.ToString() || u.Trim() == user.Name)) return true;
        }
        if (!string.IsNullOrEmpty(candidateGroups))
        {
            var groups = candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (user.UserRoles.Any(ur => ur.Role != null &&
                groups.Any(g => g.Trim() == ur.Role.Code || g.Trim() == ur.Role.Name))) return true;
        }
        return false;
    }

    private async Task<string?> DepartmentName(int? deptId)
    {
        if (!deptId.HasValue) return null;
        var dept = await _db.Departments.FindAsync(deptId.Value);
        return dept?.Name;
    }

    private async Task AddRecord(int flowId, string action, string actor, string? remark)
    {
        _db.MaterialFlowRecords.Add(new MaterialFlowRecord
        {
            FlowId = flowId,
            Action = action,
            Operator = actor,
            Comment = remark,
            OperatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static MaterialFlowDto ToDto(MaterialFlow f) => new()
    {
        Id = f.Id,
        FlowNo = f.FlowNo,
        BizType = f.BizType,
        MaterialId = f.MaterialId,
        MaterialNo = f.MaterialNo,
        MaterialName = f.MaterialName,
        Applicant = f.Applicant,
        ApplicantDept = f.ApplicantDept,
        Transferee = f.Transferee,
        TransfereeDept = f.TransfereeDept,
        Reason = f.Reason,
        Status = f.Status,
        CurrentNodeIds = f.CurrentNodeIds,
        BpmnTokens = f.BpmnTokens,
        ApplyTime = f.ApplyTime,
        Deadline = f.Deadline
    };
}
