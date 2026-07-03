using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Infrastructure.TestMaterials;

public class MaterialFlowService : IMaterialFlowService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<MaterialFlowService> _logger;
    public const string ApprovalSwitchKey = "material.transfer.approval.enabled";
    public const string MaterialBizType = "material_transfer";

    public MaterialFlowService(AppDbContext db, INotificationService notifications, ILogger<MaterialFlowService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<MaterialFlowDto> InitiateTransferAsync(InitiateTransferRequest request, int applicantId)
    {
        var material = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == request.MaterialId)
            ?? throw new BizException(4048, "测试料件不存在");
        if (material.IsDeleted) throw new BizException(4048, "测试料件不存在");
        if (material.Status != MaterialStatus.InUse)
            throw new BizException(4098, "已退回厂商的料件不能转移");

        var applicant = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == applicantId)
            ?? throw new BizException(4041, "用户不存在");
        var transferee = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TransfereeId)
            ?? throw new BizException(4041, "受让人不存在");

        var approvalEnabled = await IsApprovalEnabled();

        // 开关关闭:直接转移(仍落一条 status=approved 的流转单,保证详情时间线可追溯)
        if (!approvalEnabled)
        {
            for (var attempt = 0; ; attempt++)
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                // 防重检查放事务内，避免并发请求同时通过检查
                if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == material.Id && x.Status == "pending"))
                    throw new BizException(4056, "该料件已有进行中的流转,请勿重复发起");
                var directFlow = new MaterialFlow
                {
                    FlowNo = await NextFlowNoAsync(attempt),
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
                    DirectTransfer = true,
                    ApplyTime = DateTime.UtcNow,
                    Deadline = DateTime.UtcNow
                };
                material.CustodianId = transferee.Id;
                material.DepartmentId = transferee.DepartmentId;
                _db.MaterialFlows.Add(directFlow);
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException) when (attempt < 3)
                {
                    await tx.RollbackAsync();
                    _db.Entry(directFlow).State = EntityState.Detached;
                    continue;
                }
                await AddRecord(directFlow.Id, "direct_transfer", applicant.Name,
                    $"直接转移给 {transferee.Name}: {request.Reason}");
                await tx.CommitAsync();

                // 通知接收人（直接转移，无需审批）
                try
                {
                    await _notifications.CreateAsync(new CreateNotificationRequest
                    {
                        Type = "material_transferred",
                        Title = $"料件已转移给您：{material.Name}",
                        Body = $"料件 {material.MaterialNo}（{material.Name}）已由 {applicant.Name} 直接转移给您。备注：{request.Reason}",
                        FlowId = directFlow.Id,
                        UserId = transferee.Id,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "通知发送失败，不影响料件直接转移结果");
                }

                return ToDto(directFlow);
            }
        }

        // 开关开启:走 BPMN 审批
        var workflow = await _db.Workflows.SingleOrDefaultAsync(x => x.BizType == MaterialBizType && x.IsActive);
        if (workflow == null)
        {
            if (await _db.Workflows.AnyAsync(x => x.BizType == MaterialBizType))
                throw new BizException(4057, "流程已停用，无法发起审批");
            throw new BizException(4049, "测试料件流转流程未配置");
        }
        if (string.IsNullOrWhiteSpace(workflow.BpmnXml))
            throw new BizException(4051, "流程定义不完整,缺少 BPMN XML");

        var process = BpmnParser.Parse(workflow.BpmnXml);
        for (var attempt = 0; ; attempt++)
        {
            await using var bpmnTx = await _db.Database.BeginTransactionAsync();
            // 防重检查放事务内，避免并发请求同时通过检查
            if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == material.Id && x.Status == "pending"))
                throw new BizException(4056, "该料件已有进行中的流转,请勿重复发起");
            var flow = new MaterialFlow
            {
                FlowNo = await NextFlowNoAsync(attempt),
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
                Deadline = DateTime.UtcNow.AddDays(2),
                Context = await BuildWorkflowContext(applicant, material.ProjectId)
            };
            BpmnEngine.Start(flow, process);
            _db.MaterialFlows.Add(flow);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                await bpmnTx.RollbackAsync();
                _db.Entry(flow).State = EntityState.Detached;
                continue;
            }
            await AddRecord(flow.Id, "start", applicant.Name, request.Reason);
            await bpmnTx.CommitAsync();

            // 业务已提交，通知失败只记告警，避免把成功发起回滚成接口失败。
            try
            {
                await NotifyCurrentApproversAsync(flow, process,
                    $"您有新的料件流转待审批：{material.Name}（{material.MaterialNo}）转移给 {transferee.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "通知发送失败，不影响料件流转发起结果");
            }

            return ToDto(flow);
        }
    }

    public async Task<List<MaterialFlowDto>> PendingAsync(int userId, int? projectId = null)
    {
        var user = await LoadUser(userId);
        var isAdmin = user.UserRoles.Any(ur => ur.Role?.Code == "admin");

        // dept_admin 只能看到申请人属于其管辖部门（含子部门）的流程
        int[]? allowedDeptIds = null;
        if (!isAdmin && user.UserRoles.Any(ur => ur.Role?.Code == "dept_admin") && user.DepartmentId.HasValue)
        {
            allowedDeptIds = await DescendantDepartmentIdsAsync(user.DepartmentId.Value);
        }

        var query = _db.MaterialFlows.Where(x => x.Status == "pending");
        if (projectId.HasValue)
            query = query.Where(x => _db.TestMaterials
                .Where(m => m.ProjectId == projectId.Value)
                .Select(m => m.Id)
                .Contains(x.MaterialId));
        var flows = await query.OrderByDescending(x => x.Id).ToListAsync();

        if (allowedDeptIds != null)
        {
            var applicantIds = flows.Select(f => f.ApplicantId).Distinct().ToArray();
            var applicantDeptMap = await _db.Users
                .Where(u => applicantIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DepartmentId })
                .ToDictionaryAsync(u => u.Id, u => u.DepartmentId);
            flows = flows
                .Where(f => applicantDeptMap.TryGetValue(f.ApplicantId, out var deptId)
                            && deptId.HasValue
                            && allowedDeptIds.Contains(deptId.Value))
                .ToList();
        }

        var workflowIds = flows.SelectMany(f => f.CurrentNodeIds.Count > 0 ? new[] { f.WorkflowId } : Array.Empty<int>())
            .Distinct().Where(id => id > 0).ToArray();
        var workflowMap = await _db.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w);
        var result = new List<MaterialFlowDto>();
        foreach (var flow in flows)
        {
            if (isAdmin || await CanApprove(flow, user, workflowMap)) result.Add(ToDto(flow));
        }
        return result;
    }

    public async Task<List<MaterialFlowDto>> MineAsync(int userId, int? projectId = null)
    {
        var query = _db.MaterialFlows.Where(x => x.ApplicantId == userId);
        if (projectId.HasValue)
            query = query.Where(x => _db.TestMaterials
                .Where(m => m.ProjectId == projectId.Value)
                .Select(m => m.Id)
                .Contains(x.MaterialId));
        var flows = await query.OrderByDescending(x => x.Id).ToListAsync();
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

        // 流程完成 -> 落地业务副作用(改保管人 + 部门)
        if (flow.Status == "approved")
        {
            var material = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == flow.MaterialId)
                ?? throw new BizException(4048, "料件已不存在,无法完成转移");
            if (material.IsDeleted) throw new BizException(4048, "料件已删除,无法完成转移");
            if (material.Status != MaterialStatus.InUse)
                throw new BizException(4098, "已退回厂商的料件不能转移");
            if (flow.TransfereeId.HasValue)
            {
                material.CustodianId = flow.TransfereeId.Value;
                var transferee = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.TransfereeId.Value);
                if (transferee is not null)
                    material.DepartmentId = transferee.DepartmentId;
            }
        }

        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该流转单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "approve", user.Name, $"节点 {nodeId}: {request.Opinion}");
        await tx.CommitAsync();

        // 流程完成 → 通知申请人；未完成 → 通知下一审批节点审批人
        try
        {
            if (flow.Status == "approved")
            {
                await _notifications.CreateAsync(new CreateNotificationRequest
                {
                    Type = "material_approved",
                    Title = $"料件流转审批通过：{flow.MaterialName}",
                    Body = $"您发起的料件 {flow.MaterialNo}（{flow.MaterialName}）转移给 {flow.Transferee} 的申请已通过审批。",
                    FlowId = id,
                    UserId = flow.ApplicantId,
                });
            }
            else if (flow.Status == "pending")
            {
                var wf = await LoadWorkflow(flow.WorkflowId);
                var proc = BpmnParser.Parse(wf.BpmnXml!);
                await NotifyCurrentApproversAsync(flow, proc,
                    $"您有新的料件流转待审批：{flow.MaterialName}（{flow.MaterialNo}）转移给 {flow.Transferee}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通知发送失败，不影响料件流转审批结果");
        }

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
        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该流转单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "reject", user.Name, request.Reason);
        await tx.CommitAsync();

        // 通知申请人被驳回
        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                Type = "material_rejected",
                Title = $"料件流转审批驳回：{flow.MaterialName}",
                Body = $"您发起的料件 {flow.MaterialNo}（{flow.MaterialName}）转移给 {flow.Transferee} 的申请被驳回。原因：{request.Reason}",
                FlowId = id,
                UserId = flow.ApplicantId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通知发送失败，不影响料件流转驳回结果");
        }

        return ToDto(flow);
    }

    // ===== 私有辅助 =====
    // COUNT-then-generate 模式在高并发下存在 TOCTOU 竞态：两个请求同时 COUNT 得到相同值，
    // 生成同一 FlowNo，FlowNo 唯一索引会让其中一个抛 DbUpdateException。
    // 每次重试递增 offset 强制生成不同编号，配合调用方的 retry 循环解决。
    private async Task<string> NextFlowNoAsync(int offset = 0)
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"MF-{today:yyyyMMdd}-";
        var count = await _db.MaterialFlows.CountAsync(x => x.FlowNo.StartsWith(prefix));
        return FlowNoGenerator.Next(today, count + offset);
    }

    private async Task<bool> IsApprovalEnabled()
    {
        var setting = await _db.SystemSettings.SingleOrDefaultAsync(x => x.Key == ApprovalSwitchKey);
        return setting != null && string.Equals(setting.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<MaterialFlow> LoadFlow(int id)
        => await _db.MaterialFlows.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
           ?? throw new BizException(4010, "流转单不存在");

    private async Task<WorkflowEntity> LoadWorkflow(int id)
        => await _db.Workflows.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
           ?? throw new BizException(4049, "流程不存在");

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

    private async Task<bool> CanApprove(MaterialFlow flow, User user, Dictionary<int, WorkflowEntity>? workflowMap = null)
    {
        WorkflowEntity? workflow = null;
        BpmnProcess? process = null;

        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (flow.BpmnTokens.TryGetValue(nodeId, out var token) && token.Status == BpmnTokenStatus.Active)
            {
                if (process == null)
                {
                    if (workflowMap != null)
                        workflowMap.TryGetValue(flow.WorkflowId, out workflow);
                    else
                        workflow = await _db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.WorkflowId);
                    if (workflow?.BpmnXml == null) continue;
                    process = BpmnParser.Parse(workflow.BpmnXml);
                }
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

    // 逻辑与 WorkflowService.IsApproverForNode 对应，保持同步更新。
    // SignStates key 使用用户姓名（与 BPMN 设计器的 assignee 配置格式一致）；
    // 数据库层面应保证用户姓名唯一，否则同名用户会共用签署状态。
    private async Task<bool> IsApproverForNode(BpmnNode node, User user, MaterialFlow flow)
    {
        // 加签场景：SignStates 记录本节点各审批人是否已签，未签=仍需审批
        if (flow.BpmnTokens.TryGetValue(node.Id, out var token) &&
            token.SignStates is { Count: > 0 } &&
            token.SignStates.TryGetValue(user.Name, out var signed))
        {
            return !signed;
        }

        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "deptManager")
            {
                var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
                if (applicant?.DepartmentId is null) return false;
                var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
                var isSameDeptAdmin = user.DepartmentId == applicant.DepartmentId &&
                                      user.UserRoles.Any(ur => ur.Role?.Code == "dept_admin");
                var isDepartmentManager = department?.ManagerId == user.Id;
                return isSameDeptAdmin || isDepartmentManager;
            }
            if (assignee == "supervisor")
            {
                var approverIds = await ResolveSupervisorApproverUserIdsAsync(flow);
                return approverIds.Contains(user.Id);
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

    private async Task<int[]> DescendantDepartmentIdsAsync(int rootId)
    {
        var all = await _db.Departments.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToListAsync();
        var ids = new List<int> { rootId };
        void Walk(int parentId)
        {
            foreach (var child in all.Where(x => x.ParentId == parentId))
            {
                ids.Add(child.Id);
                Walk(child.Id);
            }
        }
        Walk(rootId);
        return ids.ToArray();
    }

    private async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(MaterialFlow flow)
    {
        var result = new List<int>();
        var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
        if (applicant?.DepartmentId is not null)
        {
            var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
            if (department?.ManagerId is not null)
            {
                result.Add(department.ManagerId.Value);
            }
        }

        // 兼容旧数据：组织节点未配置负责人时，仍可使用历史维护的直属上级。
        if (result.Count == 0 && applicant?.SupervisorId is not null)
        {
            result.Add(applicant.SupervisorId.Value);
        }

        return result;
    }

    private async Task<string?> DepartmentName(int? deptId)
    {
        if (!deptId.HasValue) return null;
        var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deptId.Value);
        return dept?.Name;
    }

    private async Task<Dictionary<string, string>> BuildWorkflowContext(User applicant, int projectId)
    {
        var roleCodes = applicant.UserRoles
            .Select(x => x.Role?.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .Cast<string>()
            .ToArray();
        var project = await _db.TestProjects
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId);

        return new Dictionary<string, string>
        {
            ["applicantRole"] = roleCodes.FirstOrDefault() ?? "",
            ["applicantRoles"] = string.Join(",", roleCodes),
            ["isProjectOwner"] = project?.OwnerId == applicant.Id ? "true" : "false"
        };
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

    private async Task NotifyCurrentApproversAsync(MaterialFlow flow, BpmnProcess process, string bodyText)
    {
        var requests = new List<CreateNotificationRequest>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) ||
                token.Status != BpmnTokenStatus.Active) continue;

            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;

            var approverIds = await ResolveApproverUserIdsAsync(node, flow);
            var nodeVersion = token.StartedAt ?? flow.ApplyTime;
            foreach (var uid in approverIds)
            {
                requests.Add(new CreateNotificationRequest
                {
                    Type = "material_approval_pending",
                    Title = $"待审批料件流转：{flow.MaterialName}",
                    Body = bodyText,
                    FlowId = flow.Id,
                    UserId = uid,
                    IdempotencyKey = NotificationIdempotencyKeys.PendingApproval("material_approval_pending", flow.Id, nodeId, uid, nodeVersion),
                });
            }
        }
        if (requests.Count > 0)
            await _notifications.CreateBatchAsync(requests);
    }

    private async Task<List<int>> ResolveApproverUserIdsAsync(BpmnNode node, MaterialFlow flow)
    {
        var result = new List<int>();
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "deptManager")
            {
                var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
                if (applicant?.DepartmentId is not null)
                {
                    var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
                    if (dept?.ManagerId is not null) result.Add(dept.ManagerId.Value);
                    var deptAdmins = await _db.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .Where(u => u.DepartmentId == applicant.DepartmentId &&
                                    u.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "dept_admin"))
                        .Select(u => u.Id).ToListAsync();
                    foreach (var uid in deptAdmins)
                        if (!result.Contains(uid)) result.Add(uid);
                }
            }
            else if (assignee == "supervisor")
            {
                foreach (var supervisorId in await ResolveSupervisorApproverUserIdsAsync(flow))
                {
                    if (!result.Contains(supervisorId)) result.Add(supervisorId);
                }
            }
            else if (int.TryParse(assignee, out var uid)) result.Add(uid);
            else
            {
                var u = await _db.Users.FirstOrDefaultAsync(x => x.Name == assignee || x.EmployeeNo == assignee);
                if (u is not null) result.Add(u.Id);
            }
        }

        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (int.TryParse(p, out var uid)) { if (!result.Contains(uid)) result.Add(uid); }
                else
                {
                    var u = await _db.Users.FirstOrDefaultAsync(x => x.Name == p);
                    if (u is not null && !result.Contains(u.Id)) result.Add(u.Id);
                }
            }
        }

        if (!string.IsNullOrEmpty(candidateGroups))
        {
            var groups = candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToArray();
            var groupUsers = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => ur.Role != null &&
                    groups.Any(g => g == ur.Role.Code || g == ur.Role.Name)))
                .Select(u => u.Id).ToListAsync();
            foreach (var uid in groupUsers)
                if (!result.Contains(uid)) result.Add(uid);
        }

        return result;
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
        DirectTransfer = f.DirectTransfer,
        CurrentNodeIds = f.CurrentNodeIds,
        BpmnTokens = f.BpmnTokens,
        ApplyTime = f.ApplyTime,
        Deadline = f.Deadline
    };
}
