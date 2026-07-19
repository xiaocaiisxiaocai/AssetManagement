using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Services;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Workflow;
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
            .SingleOrDefaultAsync(x => x.Id == applicantId && x.IsActive)
            ?? throw new BizException(4041, "用户不存在或已停用");
        await EnsureMaterialInScopeAsync(material, applicant);
        var isSupervisor = applicant.UserRoles.Any(x => x.Role is { Code: "supervisor", IsActive: true });
        var isProjectOwner = await _db.TestProjects.AnyAsync(x => x.Id == material.ProjectId && x.OwnerId == applicantId);
        if (!isSupervisor && material.CustodianId != applicantId && !isProjectOwner)
            throw new BizException(4047, "只能流转本人保管或本人负责项目的料件");
        var transferee = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TransfereeId && x.IsActive)
            ?? throw new BizException(4041, "受让人不存在或已停用");
        if (transferee.Id == applicant.Id) throw new BizException(4001, "接收人不能与申请人相同");
        if (transferee.Id == material.CustodianId) throw new BizException(4001, "接收人不能是当前保管人");

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
                material.RowVersion++;
                _db.MaterialFlows.Add(directFlow);
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new BizException(4090, "料件已被其他操作转移，请刷新后重试");
                }
                catch (DbUpdateException) when (attempt < 3)
                {
                    await tx.RollbackAsync();
                    _db.Entry(directFlow).State = EntityState.Detached;
                    await _db.Entry(material).ReloadAsync();
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

                return await ToDtoAsync(directFlow);
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
                Context = await BuildWorkflowContext(applicant, material.ProjectId, process)
            };
            BpmnEngine.Start(flow, process);
            await NormalizeSignStatesAsync(flow, process);
            await EnsureCurrentApproversResolvableAsync(flow, process);
            flow.ActiveScopeKey = flow.Status == "pending" ? $"material:{material.Id}" : null;
            _db.MaterialFlows.Add(flow);
            if (flow.Status == "approved")
                await ApplyMaterialTransferAsync(flow);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                await bpmnTx.RollbackAsync();
                _db.Entry(flow).State = EntityState.Detached;
                if (await _db.MaterialFlows.AnyAsync(x => x.MaterialId == material.Id && x.Status == "pending"))
                    throw new BizException(4056, "该料件已有进行中的流转,请勿重复发起");
                await _db.Entry(material).ReloadAsync();
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

            return await ToDtoAsync(flow);
        }
    }

    public async Task<List<MaterialFlowDto>> PendingAsync(int userId, int? projectId = null)
    {
        var user = await LoadUser(userId);
        var isAdmin = IsAdmin(user);

        // supervisor 只能看到申请人属于其管辖部门（含子部门）的流程
        int[]? allowedDeptIds = null;
        if (!isAdmin && IsSupervisor(user) && !user.DepartmentId.HasValue)
            return new List<MaterialFlowDto>();
        if (!isAdmin && IsSupervisor(user) && user.DepartmentId.HasValue)
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
            var actionableNodeIds = await GetActionableNodeIdsAsync(flow, user, workflowMap);
            if (actionableNodeIds.Count > 0) result.Add(await ToDtoAsync(flow, actionableNodeIds));
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
        var result = new List<MaterialFlowDto>();
        foreach (var flow in flows) result.Add(await ToDtoAsync(flow));
        return result;
    }

    public async Task<MaterialFlowDto> GetAsync(int id, int userId)
    {
        var flow = await LoadFlow(id);
        var user = await LoadUser(userId);
        await EnsureCanViewFlowAsync(flow, user);
        return await ToDtoAsync(flow, await GetActionableNodeIdsAsync(flow, user));
    }

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
        BpmnEngine.Approve(flow, process, nodeId, ApprovalIdentity(flow, nodeId, user), request.Opinion);

        if (flow.Status == "pending")
            await EnsureCurrentApproversResolvableAsync(flow, process);

        // 流程完成 -> 落地业务副作用(改保管人 + 部门)
        if (flow.Status == "approved")
        {
            flow.ActiveScopeKey = null;
            await ApplyMaterialTransferAsync(flow);
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

        return await ToDtoAsync(flow);
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
        flow.ActiveScopeKey = null;
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

        return await ToDtoAsync(flow);
    }

    public async Task<MaterialFlowDto> WithdrawAsync(int id, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        if (flow.ApplicantId != userId)
            throw new BizException(4031, "只有申请人本人可以撤回该申请");

        var applicant = await LoadUser(userId);
        await using var tx = await _db.Database.BeginTransactionAsync();
        BpmnEngine.Withdraw(flow, applicant.Name);
        flow.ActiveScopeKey = null;
        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该流转单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "withdraw", applicant.Name, "申请人主动撤回");
        await tx.CommitAsync();

        return await ToDtoAsync(flow);
    }

    // ===== 私有辅助 =====
    // COUNT-then-generate 模式在高并发下存在 TOCTOU 竞态：两个请求同时 COUNT 得到相同值，
    // 生成同一 FlowNo，FlowNo 唯一索引会让其中一个抛 DbUpdateException。
    // 每次重试递增 offset 强制生成不同编号，配合调用方的 retry 循环解决。
    private async Task<string> NextFlowNoAsync(int offset = 0)
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"MF-{today:yyyyMMdd}-";
        var existing = await _db.MaterialFlows
            .Where(x => x.FlowNo.StartsWith(prefix))
            .Select(x => x.FlowNo)
            .ToListAsync();
        var maxSequence = existing
            .Select(x => int.TryParse(x[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();
        return FlowNoGenerator.Next(today, maxSequence + offset);
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
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id && u.IsActive)
            ?? throw new BizException(4041, "用户不存在或已停用");
        if (!user.UserRoles.Any(ur => ur.Role is { IsActive: true }))
            throw new BizException(4012, "账号角色已禁用，请重新登录");
        return user;
    }

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

    private async Task<List<string>> GetActionableNodeIdsAsync(
        MaterialFlow flow,
        User user,
        Dictionary<int, WorkflowEntity>? workflowMap = null)
    {
        WorkflowEntity? workflow;
        if (workflowMap != null)
            workflowMap.TryGetValue(flow.WorkflowId, out workflow);
        else
            workflow = await _db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.WorkflowId);
        if (string.IsNullOrWhiteSpace(workflow?.BpmnXml)) return new List<string>();

        var process = BpmnParser.Parse(workflow.BpmnXml);
        var result = new List<string>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) || token.Status != BpmnTokenStatus.Active)
                continue;
            var node = process.FindNode(nodeId);
            if (node?.Type == BpmnNodeType.UserTask && await IsApproverForNode(node, user, flow))
                result.Add(nodeId);
        }
        return result;
    }

    private async Task<bool> CanApprove(MaterialFlow flow, User user)
        => (await GetActionableNodeIdsAsync(flow, user)).Count > 0;

    private async Task EnsureCanApproveNode(MaterialFlow flow, string nodeId, User user)
    {
        if (!flow.CurrentNodeIds.Contains(nodeId)) throw new BizException(4014, "该节点当前不可审批");
        var workflow = await LoadWorkflow(flow.WorkflowId);
        var process = BpmnParser.Parse(workflow.BpmnXml!);
        var node = process.FindNode(nodeId);
        if (node == null || node.Type != BpmnNodeType.UserTask) throw new BizException(4015, "无效的审批节点");
        if (!await IsApproverForNode(node, user, flow)) throw new BizException(4016, "您无权审批此节点");
    }

    // 逻辑与 WorkflowService.IsApproverForNode 对应，保持同步更新。
    // SignStates key 使用用户姓名（与 BPMN 设计器的 assignee 配置格式一致）；
    // 数据库层面应保证用户姓名唯一，否则同名用户会共用签署状态。
    private async Task<bool> IsApproverForNode(BpmnNode node, User user, MaterialFlow flow)
    {
        // 加签场景：SignStates 记录本节点各审批人是否已签，未签=仍需审批
        if (flow.BpmnTokens.TryGetValue(node.Id, out var token) && token.SignStates is { Count: > 0 })
        {
            var identity = TryApprovalIdentity(token, user);
            return identity != null && !token.SignStates[identity];
        }

        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
            {
                var approverIds = await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(
                    _db, flow.ApplicantId, assignee);
                return approverIds.Contains(user.Id);
            }
            if (assignee == "deptManager")
            {
                var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
                if (applicant?.DepartmentId is null) return false;
                var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
                var isSameDeptAdmin = user.DepartmentId == applicant.DepartmentId &&
                                      user.UserRoles.Any(ur => ur.Role is { Code: "supervisor", IsActive: true });
                var isDepartmentManager = department?.ManagerId == user.Id;
                return isSameDeptAdmin || isDepartmentManager;
            }
            if (assignee == "supervisor")
            {
                var approverIds = await ResolveSupervisorApproverUserIdsAsync(flow);
                return approverIds.Contains(user.Id);
            }
            var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, assignee);
            return resolution.IsResolved && resolution.UserIds.Contains(user.Id);
        }
        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var candidateUser in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, candidateUser);
                if (resolution.IsResolved && resolution.UserIds.Contains(user.Id)) return true;
            }
        }
        if (!string.IsNullOrEmpty(candidateGroups))
        {
            foreach (var candidateGroup in candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(_db, candidateGroup);
                if (resolution.IsResolved && resolution.UserIds.Contains(user.Id)) return true;
            }
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

    private static bool IsAdmin(User user)
        => user.UserRoles.Any(ur => ur.Role is { Code: "admin", IsActive: true });

    private static bool IsSupervisor(User user)
        => user.UserRoles.Any(ur => ur.Role is { Code: "supervisor", IsActive: true });

    private async Task EnsureMaterialInScopeAsync(TestMaterial material, User user)
    {
        if (IsAdmin(user) || !IsSupervisor(user)) return;
        if (!user.DepartmentId.HasValue)
            throw new BizException(4048, "测试料件不存在");
        var allowed = await DescendantDepartmentIdsAsync(user.DepartmentId.Value);
        if (!material.DepartmentId.HasValue || !allowed.Contains(material.DepartmentId.Value))
            throw new BizException(4048, "测试料件不存在");
    }

    private async Task EnsureCanViewFlowAsync(MaterialFlow flow, User user)
    {
        if (IsAdmin(user) || flow.ApplicantId == user.Id || flow.TransfereeId == user.Id)
            return;
        if (await CanApprove(flow, user)) return;
        if (IsSupervisor(user) && user.DepartmentId.HasValue)
        {
            var material = await _db.TestMaterials.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.MaterialId);
            var allowed = await DescendantDepartmentIdsAsync(user.DepartmentId.Value);
            if (material?.DepartmentId is int departmentId && allowed.Contains(departmentId)) return;
        }
        throw new BizException(4030, "无权查看该流转单");
    }

    private async Task ApplyMaterialTransferAsync(MaterialFlow flow)
    {
        var material = await _db.TestMaterials.AsTracking().SingleOrDefaultAsync(x => x.Id == flow.MaterialId)
            ?? throw new BizException(4048, "料件已不存在,无法完成转移");
        if (material.IsDeleted) throw new BizException(4048, "料件已删除,无法完成转移");
        if (material.Status != MaterialStatus.InUse)
            throw new BizException(4098, "已退回厂商的料件不能转移");
        if (!flow.TransfereeId.HasValue)
            throw new BizException(4001, "流转单缺少接收人");
        material.CustodianId = flow.TransfereeId.Value;
        var transferee = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == flow.TransfereeId.Value && x.IsActive)
            ?? throw new BizException(4041, "接收人不存在或已停用");
        material.DepartmentId = transferee.DepartmentId;
        material.RowVersion++;
    }

    private async Task NormalizeSignStatesAsync(MaterialFlow flow, BpmnProcess process)
    {
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask ||
                !node.Properties.TryGetValue("approvalMode", out var mode) || mode != "all" ||
                !flow.BpmnTokens.TryGetValue(nodeId, out var token)) continue;
            var approverIds = await ResolveApproverUserIdsAsync(node, flow);
            if (approverIds.Count == 0)
                throw new BizException(4051, $"会签节点 {node.Name} 未解析到有效审批人");
            token.SignStates = approverIds.Distinct().ToDictionary(x => x.ToString(), _ => false);
        }
    }

    private async Task EnsureCurrentApproversResolvableAsync(MaterialFlow flow, BpmnProcess process)
    {
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;
            if ((await ResolveApproverUserIdsAsync(node, flow)).Count > 0) continue;

            var assignee = node.Properties.GetValueOrDefault("assignee");
            if (assignee == "supervisor")
                throw new BizException(4051, "申请人未配置直属主管，无法发起审批");
            if (assignee == "deptManager")
                throw new BizException(4051, $"审批节点“{node.Name}”未配置有效部门负责人");
            if (assignee == OrganizationApprovalResolver.SectionManagerAssignee)
                throw new BizException(4051, "申请人所属课未配置有效课级负责人");
            if (assignee == OrganizationApprovalResolver.DepartmentManagerAssignee)
                throw new BizException(4051, "申请人所属部门未配置有效部门负责人");
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
                throw new BizException(4051, $"审批节点“{node.Name}”未解析到有效的组织层级负责人");
            throw new BizException(4051, $"审批节点“{node.Name}”未配置唯一且有效的审批人，请在流程设计器重新选择");
        }
    }

    private static string ApprovalIdentity(MaterialFlow flow, string nodeId, User user)
    {
        if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) || token.SignStates is not { Count: > 0 })
            return user.Id.ToString();
        return TryApprovalIdentity(token, user)
               ?? throw new BizException(4016, "您不在该节点的会签人列表中");
    }

    private static string? TryApprovalIdentity(BpmnToken token, User user)
    {
        var userId = user.Id.ToString();
        return token.SignStates!.ContainsKey(userId) ? userId : null;
    }

    private async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(MaterialFlow flow)
    {
        var result = new List<int>();
        var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
        if (applicant?.DepartmentId is not null)
        {
            var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
            if (department?.ManagerId is int managerId &&
                await _db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
            {
                result.Add(managerId);
            }
        }

        // 兼容旧数据：组织节点未配置负责人时，仍可使用历史维护的直属上级。
        if (result.Count == 0 && applicant?.SupervisorId is int supervisorId &&
            await _db.Users.AsNoTracking().AnyAsync(x => x.Id == supervisorId && x.IsActive))
        {
            result.Add(supervisorId);
        }

        return result;
    }

    private async Task<string?> DepartmentName(int? deptId)
    {
        if (!deptId.HasValue) return null;
        var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deptId.Value);
        return dept?.Name;
    }

    private async Task<Dictionary<string, string>> BuildWorkflowContext(
        User applicant,
        int projectId,
        BpmnProcess process)
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
        var context = new Dictionary<string, string>
        {
            ["applicantRole"] = roleCodes.FirstOrDefault() ?? "",
            ["applicantRoles"] = string.Join(",", roleCodes),
            ["isProjectOwner"] = project?.OwnerId == applicant.Id ? "true" : "false"
        };
        if (!OrganizationApprovalResolver.IsUsedBy(process)) return context;

        foreach (var levelCode in OrganizationApprovalResolver.GetRequestedLevelCodes(process))
        {
            var target = await OrganizationApprovalResolver.ResolveTargetAsync(_db, applicant.Id, levelCode);
            var value = target.RequiresApproval ? "true" : "false";
            context[OrganizationApprovalResolver.ApprovalConditionKey(levelCode)] = value;
            if (levelCode == "section") context["requiresSectionApproval"] = value;
            if (levelCode == "department") context["requiresDepartmentApproval"] = value;
        }
        return context;
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
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
            {
                result.AddRange(await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(
                    _db, flow.ApplicantId, assignee));
            }
            else if (assignee == "deptManager")
            {
                var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
                if (applicant?.DepartmentId is not null)
                {
                    var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
                    if (dept?.ManagerId is int managerId &&
                        await _db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
                        result.Add(managerId);
                    var deptAdmins = await _db.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .Where(u => u.IsActive && u.DepartmentId == applicant.DepartmentId &&
                                    u.UserRoles.Any(ur => ur.Role != null && ur.Role.IsActive && ur.Role.Code == "supervisor"))
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
            else
            {
                await AddExplicitApproverUserIds(result, assignee);
            }
        }

        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                await AddExplicitApproverUserIds(result, part.Trim());
            }
        }

        if (!string.IsNullOrEmpty(candidateGroups))
        {
            foreach (var group in candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveGroupUsersAsync(_db, group);
                EnsureUnambiguousResolution(resolution);
                foreach (var uid in resolution.UserIds)
                    if (!result.Contains(uid)) result.Add(uid);
            }
        }

        return result;
    }

    private async Task AddExplicitApproverUserIds(List<int> result, string value)
    {
        var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, value);
        EnsureUnambiguousResolution(resolution);
        foreach (var userId in resolution.UserIds)
            if (!result.Contains(userId)) result.Add(userId);
    }

    private static void EnsureUnambiguousResolution(ApproverIdentityResolution resolution)
    {
        if (resolution.Status == ApproverIdentityResolutionStatus.Ambiguous)
            throw new BizException(4051, $"审批人配置存在歧义，请在流程设计器重新选择。{resolution.Diagnostic}");
    }

    private async Task<MaterialFlowDto> ToDtoAsync(
        MaterialFlow flow,
        IEnumerable<string>? actionableNodeIds = null)
    {
        var dto = ToDto(flow, actionableNodeIds);
        var workflow = await _db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.WorkflowId);
        if (string.IsNullOrWhiteSpace(workflow?.BpmnXml)) return dto;
        var process = BpmnParser.Parse(workflow.BpmnXml);

        var completed = new List<WorkflowProgressStepDto>();
        foreach (var token in flow.BpmnTokens.Values
                     .Where(x => x.Status == BpmnTokenStatus.Completed)
                     .OrderBy(x => x.CompletedAt))
        {
            var node = process.FindNode(token.NodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;
            completed.Add(await BuildProgressStepAsync(node, flow, token, "completed", false));
        }

        var current = new List<WorkflowProgressStepDto>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;
            flow.BpmnTokens.TryGetValue(nodeId, out var token);
            current.Add(await BuildProgressStepAsync(node, flow, token, "current", false));
        }

        var next = new List<WorkflowProgressStepDto>();
        if (flow.Status == "pending")
            foreach (var candidate in FindNextUserTasks(process, flow.CurrentNodeIds))
                next.Add(await BuildProgressStepAsync(candidate.Node, flow, null, "next", candidate.IsPossible));

        dto.ProgressSteps = completed.Concat(current).Concat(next).ToList();
        dto.CurrentSteps = current;
        dto.NextSteps = next;
        return dto;
    }

    private async Task<WorkflowProgressStepDto> BuildProgressStepAsync(
        BpmnNode node,
        MaterialFlow flow,
        BpmnToken? token,
        string state,
        bool isPossible)
    {
        var userIds = state == "completed"
            ? ParseCompletedApproverIds(token)
            : await ResolveApproverUserIdsAsync(node, flow);
        if (token?.SignStates is { Count: > 0 })
            userIds = token.SignStates.Keys.Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0).Distinct().ToList();

        var users = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.EmployeeNo, x.Name })
            .ToListAsync();
        var completedBy = token?.Approver;
        if (int.TryParse(completedBy, out var completedById))
            completedBy = users.FirstOrDefault(x => x.Id == completedById)?.Name ?? completedBy;

        return new WorkflowProgressStepDto
        {
            NodeId = node.Id,
            NodeName = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name,
            State = state,
            IsPossible = isPossible,
            StartedAt = token?.StartedAt,
            CompletedAt = token?.CompletedAt,
            CompletedBy = completedBy,
            Opinion = token?.Opinion,
            Assignees = users.Select(user => new WorkflowAssigneeDto
            {
                UserId = user.Id,
                EmployeeNo = user.EmployeeNo,
                Name = user.Name,
                Status = ResolveAssigneeStatus(token, user.Id, user.Name, state)
            }).ToList()
        };
    }

    private static string ResolveAssigneeStatus(BpmnToken? token, int userId, string userName, string stepState)
    {
        var isRejected = token?.Opinion?.StartsWith("[驳回]", StringComparison.Ordinal) == true;
        if (isRejected && string.Equals(token?.Approver, userName, StringComparison.Ordinal)) return "rejected";
        if (token?.SignStates is { Count: > 0 })
            return token.SignStates.GetValueOrDefault(userId.ToString()) ? "completed" : "skipped";
        if (isRejected) return "rejected";
        return stepState == "completed" ? "completed" : "pending";
    }

    private static List<int> ParseCompletedApproverIds(BpmnToken? token)
    {
        var result = new List<int>();
        if (int.TryParse(token?.Approver, out var id)) result.Add(id);
        if (token?.SignStates is not null)
            result.AddRange(token.SignStates.Keys.Select(x => int.TryParse(x, out var value) ? value : 0).Where(x => x > 0));
        return result.Distinct().ToList();
    }

    private static List<(BpmnNode Node, bool IsPossible)> FindNextUserTasks(
        BpmnProcess process,
        IEnumerable<string> currentNodeIds)
    {
        var result = new Dictionary<string, (BpmnNode Node, bool IsPossible)>();
        var queue = new Queue<(string NodeId, bool IsPossible)>();
        foreach (var currentNodeId in currentNodeIds)
        {
            var outgoing = process.GetOutgoingFlows(currentNodeId);
            foreach (var edge in outgoing)
                queue.Enqueue((edge.TargetRef, outgoing.Count > 1 || !string.IsNullOrWhiteSpace(edge.ConditionExpression)));
        }
        var visited = new HashSet<(string, bool)>();
        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            if (!visited.Add((item.NodeId, item.IsPossible))) continue;
            var node = process.FindNode(item.NodeId);
            if (node is null) continue;
            if (node.Type == BpmnNodeType.UserTask)
            {
                if (!result.TryGetValue(node.Id, out var existing) || existing.IsPossible && !item.IsPossible)
                    result[node.Id] = (node, item.IsPossible);
                continue;
            }
            if (node.Type == BpmnNodeType.EndEvent) continue;
            var outgoing = process.GetOutgoingFlows(node.Id);
            var branchIsPossible = item.IsPossible ||
                                   node.Type is BpmnNodeType.ExclusiveGateway or BpmnNodeType.InclusiveGateway ||
                                   outgoing.Count > 1;
            foreach (var edge in outgoing)
                queue.Enqueue((edge.TargetRef, branchIsPossible || !string.IsNullOrWhiteSpace(edge.ConditionExpression)));
        }
        return result.Values.ToList();
    }

    private static MaterialFlowDto ToDto(MaterialFlow f, IEnumerable<string>? actionableNodeIds = null) => new()
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
        ActionableNodeIds = actionableNodeIds?.ToList() ?? new List<string>(),
        BpmnTokens = f.BpmnTokens,
        ApplyTime = f.ApplyTime,
        Deadline = f.Deadline
    };
}
