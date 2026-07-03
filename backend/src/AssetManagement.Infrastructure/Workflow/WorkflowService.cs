using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

namespace AssetManagement.Infrastructure.Workflow;

public class WorkflowService : IWorkflowService
{
    private readonly AppDbContext _db;
    private readonly IBizEffectApplier _bizEffectApplier;
    private readonly INotificationService _notifications;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(AppDbContext db, IBizEffectApplier bizEffectApplier, INotificationService notifications, ILogger<WorkflowService> logger)
    {
        _db = db;
        _bizEffectApplier = bizEffectApplier;
        _notifications = notifications;
        _logger = logger;
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
        _db.Entry(workflow).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task<WorkflowDto> SetWorkflowStatusAsync(int id, bool isActive)
    {
        var workflow = await LoadWorkflow(id);
        if (isActive)
        {
            await EnsureNoOtherActiveWorkflowForBizType(workflow.BizType, workflow.Id);
        }
        workflow.IsActive = isActive;
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
        if (await _db.MaterialFlows.AnyAsync(x => x.WorkflowId == id))
        {
            throw new BizException(4093, "已有料件流转实例使用该流程，不能删除");
        }

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();
    }

    public async Task<ApprovalFlowDto> StartAsync(StartApprovalRequest request, int applicantId)
    {
        var workflow = await _db.Workflows.SingleOrDefaultAsync(x => x.BizType == request.BizType && x.IsActive);
        if (workflow == null)
        {
            if (await _db.Workflows.AnyAsync(x => x.BizType == request.BizType))
            {
                throw new BizException(4057, "流程已停用，无法发起审批");
            }
            throw new BizException(4049, "流程不存在");
        }

        if (string.IsNullOrWhiteSpace(workflow.BpmnXml))
            throw new BizException(4051, "流程定义不完整，缺少 BPMN XML");

        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == request.AssetId)
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

        var applicant = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == applicantId)
            ?? throw new BizException(4041, "用户不存在");
        var transferee = request.TransfereeId.HasValue
            ? await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TransfereeId.Value)
            : null;

        // 解析 BPMN 流程定义
        var bpmnProcess = BpmnParser.Parse(workflow.BpmnXml);

        await using var tx = await _db.Database.BeginTransactionAsync();

        // 防重检查放事务内，防止并发请求同时通过检查后各自插入
        if (await _db.ApprovalFlows.AnyAsync(x => x.AssetId == asset.Id && x.Status == "pending"))
            throw new BizException(4056, "该资产已有进行中的审批,请勿重复发起");

        var flow = new ApprovalFlow
        {
            FlowNo = $"APV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
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
            Deadline = DateTime.UtcNow.AddDays(2),
            Context = BuildWorkflowContext(applicant)
        };

        // 启动 BPMN 流程引擎
        BpmnEngine.Start(flow, bpmnProcess);

        _db.ApprovalFlows.Add(flow);
        await _db.SaveChangesAsync();
        await AddRecord(flow.Id, "start", applicant.Name, request.Reason);
        await tx.CommitAsync();

        // 业务已提交，通知失败只记告警，避免把成功发起回滚成接口失败。
        try
        {
            await NotifyCurrentApproversAsync(flow, bpmnProcess, $"您有新的待审批任务：{asset.Name} 的{BizTypeLabel(workflow.BizType)}申请");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通知发送失败，不影响审批发起结果");
        }

        return ToFlowDto(flow);
    }

    public async Task<List<ApprovalFlowDto>> PendingAsync(int userId)
    {
        var user = await LoadUser(userId);
        var isAdmin = IsAdmin(user);

        // dept_admin 只能看到自己管辖部门相关的流程；转让接收节点还要看接收人部门。
        int[]? allowedDeptIds = null;
        if (!isAdmin && user.UserRoles.Any(ur => ur.Role?.Code == "dept_admin") && user.DepartmentId.HasValue)
        {
            allowedDeptIds = await DescendantDepartmentIdsAsync(user.DepartmentId.Value);
        }

        var flows = await _db.ApprovalFlows
            .Where(x => x.Status == "pending")
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        if (allowedDeptIds != null)
        {
            var relatedUserIds = flows
                .SelectMany(f => new[] { f.ApplicantId, f.TransfereeId ?? 0 })
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            var userDeptMap = await _db.Users
                .Where(u => relatedUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DepartmentId })
                .ToDictionaryAsync(u => u.Id, u => u.DepartmentId);
            flows = flows
                .Where(f => IsDeptInScope(userDeptMap.GetValueOrDefault(f.ApplicantId), allowedDeptIds)
                            || (f.TransfereeId.HasValue && IsDeptInScope(userDeptMap.GetValueOrDefault(f.TransfereeId.Value), allowedDeptIds)))
                .ToList();
        }

        // 预取所有涉及的工作流定义，避免 N+1 查询
        var workflowIds = flows.Select(f => f.WorkflowId).Distinct().ToArray();
        var workflowMap = await _db.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w);

        // 筛选当前用户可以审批的流程
        var result = new List<ApprovalFlowDto>();
        foreach (var flow in flows)
        {
            if (isAdmin || await CanApprove(flow, user, workflowMap))
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
            await _bizEffectApplier.ApplyAsync(flow, userId);
        }

        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该审批单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "approve", user.Name, $"节点 {nodeId}: {request.Opinion}");
        await tx.CommitAsync();

        // 流程完成 → 通知申请人；未完成 → 通知下一审批节点的审批人
        try
        {
            if (flow.Status == "approved")
            {
                await _notifications.CreateAsync(new CreateNotificationRequest
                {
                    Type = "approval_approved",
                    Title = $"审批通过：{flow.AssetName}",
                    Body = $"您发起的 {flow.AssetName}（{flow.AssetNo}）{BizTypeLabel(flow.BizType)}申请已通过审批。",
                    FlowId = id,
                    UserId = flow.ApplicantId,
                });
            }
            else if (flow.Status == "pending")
            {
                await NotifyCurrentApproversAsync(flow, bpmnProcess, $"您有新的待审批任务：{flow.AssetName} 的{BizTypeLabel(flow.BizType)}申请");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通知发送失败，不影响审批结果");
        }

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

        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该审批单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "reject", user.Name, request.Reason);
        await tx.CommitAsync();

        // 通知申请人审批被驳回
        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                Type = "approval_rejected",
                Title = $"审批驳回：{flow.AssetName}",
                Body = $"您发起的 {flow.AssetName}（{flow.AssetNo}）{BizTypeLabel(flow.BizType)}申请被驳回。原因：{request.Reason}",
                FlowId = id,
                UserId = flow.ApplicantId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通知发送失败，不影响审批结果");
        }

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

        await using var tx = await _db.Database.BeginTransactionAsync();
        flow.ConfirmedAt = DateTime.UtcNow;

        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == flow.AssetId);
        if (asset != null)
        {
            asset.Status = AssetStatus.Available;
            asset.CustodianId = null;
        }

        await _db.SaveChangesAsync();
        await AddRecord(id, "confirm_return", user.Name, "确认归还入库");
        await tx.CommitAsync();

        // 通知借用人资产已确认归还
        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                Type = "return_confirmed",
                Title = $"归还确认：{flow.AssetName}",
                Body = $"您借用的 {flow.AssetName}（{flow.AssetNo}）已确认归还入库。",
                FlowId = id,
                UserId = flow.ApplicantId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "通知发送失败，不影响入库结果");
        }

        return ToFlowDto(flow);
    }

    // ========== 私有辅助方法 ==========

    private async Task<WorkflowEntity> LoadWorkflow(int id)
        => await _db.Workflows.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
           ?? throw new BizException(4049, "流程不存在");

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

        if (await _db.Workflows.AnyAsync(x => x.Name == name && x.Id != workflow.Id))
        {
            throw new BizException(4094, "流程名称已存在");
        }

        await EnsureNoOtherActiveWorkflowForBizType(bizType, workflow.Id);

        ValidateBpmnXml(request.BpmnXml);

        workflow.Name = name;
        workflow.BizType = bizType;
        workflow.BpmnXml = request.BpmnXml;
    }

    private async Task EnsureNoOtherActiveWorkflowForBizType(string bizType, int workflowId)
    {
        if (await _db.Workflows.AnyAsync(x => x.BizType == bizType && x.Id != workflowId && x.IsActive))
        {
            throw new BizException(4094, "业务类型已有启用流程");
        }
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
        => await _db.ApprovalFlows.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
           ?? throw new BizException(4010, "审批工单不存在");

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

    private async Task<bool> CanApprove(ApprovalFlow flow, User user, Dictionary<int, WorkflowEntity>? workflowMap = null)
    {
        // 检查用户是否可以审批流程中的任一活跃节点
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (flow.BpmnTokens.TryGetValue(nodeId, out var token) && token.Status == BpmnTokenStatus.Active)
            {
                WorkflowEntity? workflow;
                if (workflowMap != null)
                    workflowMap.TryGetValue(flow.WorkflowId, out workflow);
                else
                    workflow = await _db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.WorkflowId);
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
                var targetDeptId = await ResolveDeptManagerTargetDepartmentIdAsync(node, flow);
                if (targetDeptId is null)
                {
                    return false;
                }

                var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetDeptId.Value);
                var isSameDeptAdmin = user.DepartmentId == targetDeptId &&
                                      user.UserRoles.Any(ur => ur.Role?.Code == "dept_admin");
                var isDepartmentManager = department?.ManagerId == user.Id;
                return isSameDeptAdmin || isDepartmentManager;
            }
            else if (assignee == "supervisor")
            {
                var approverIds = await ResolveSupervisorApproverUserIdsAsync(flow);
                return approverIds.Contains(user.Id);
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

    /// <summary>
    /// 通知流程当前所有活跃 UserTask 节点对应的审批人
    /// </summary>
    private async Task NotifyCurrentApproversAsync(ApprovalFlow flow, BpmnProcess process, string bodyPrefix)
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
            foreach (var approverUserId in approverIds)
            {
                requests.Add(new CreateNotificationRequest
                {
                    Type = "approval_pending",
                    Title = $"待审批：{flow.AssetName}",
                    Body = bodyPrefix,
                    FlowId = flow.Id,
                    UserId = approverUserId,
                    IdempotencyKey = NotificationIdempotencyKeys.PendingApproval("approval_pending", flow.Id, nodeId, approverUserId, nodeVersion),
                });
            }
        }
        if (requests.Count > 0)
            await _notifications.CreateBatchAsync(requests);
    }

    /// <summary>
    /// 解析 BPMN UserTask 节点的审批人列表，返回用户 ID 集合
    /// </summary>
    private async Task<List<int>> ResolveApproverUserIdsAsync(BpmnNode node, ApprovalFlow flow)
    {
        var result = new List<int>();
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "deptManager")
            {
                var targetDeptId = await ResolveDeptManagerTargetDepartmentIdAsync(node, flow);
                if (targetDeptId is not null)
                {
                    var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetDeptId.Value);
                    if (dept?.ManagerId is not null) result.Add(dept.ManagerId.Value);
                    var deptAdmins = await _db.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .Where(u => u.DepartmentId == targetDeptId &&
                                    u.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "dept_admin"))
                        .Select(u => u.Id)
                        .ToListAsync();
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
            else if (int.TryParse(assignee, out var uid))
            {
                result.Add(uid);
            }
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
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var uid in groupUsers)
                if (!result.Contains(uid)) result.Add(uid);
        }

        return result;
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

    private async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(ApprovalFlow flow)
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

    private static Dictionary<string, string> BuildWorkflowContext(User applicant)
    {
        var roleCodes = applicant.UserRoles
            .Select(x => x.Role?.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .Cast<string>()
            .ToArray();

        return new Dictionary<string, string>
        {
            ["applicantRole"] = roleCodes.FirstOrDefault() ?? "",
            ["applicantRoles"] = string.Join(",", roleCodes)
        };
    }

    private async Task<int?> ResolveDeptManagerTargetDepartmentIdAsync(BpmnNode node, ApprovalFlow flow)
    {
        if (flow.BizType == "transfer" && node.Id == "Task_receiver" && flow.TransfereeId.HasValue)
        {
            var transferee = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.TransfereeId.Value);
            return transferee?.DepartmentId;
        }

        var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
        return applicant?.DepartmentId;
    }

    private static bool IsDeptInScope(int? deptId, int[] allowedDeptIds)
        => deptId.HasValue && allowedDeptIds.Contains(deptId.Value);

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

    private static WorkflowDto ToWorkflowDto(WorkflowEntity w)
    {
        var errors = ValidateWorkflowBpmnForStatus(w.BpmnXml);
        return new WorkflowDto
        {
            Id = w.Id,
            Name = w.Name,
            BizType = w.BizType,
            BizTypeLabel = BizTypeLabel(w.BizType),
            BpmnXml = w.BpmnXml,
            IsActive = w.IsActive,
            BpmnStatus = string.IsNullOrWhiteSpace(w.BpmnXml)
                ? "empty"
                : errors.Count == 0 ? "configured" : "invalid",
            BpmnValidationErrors = errors
        };
    }

    private static string BizTypeLabel(string bizType)
        => bizType switch
        {
            "borrow" => "资产借用",
            "transfer" => "资产转让",
            "return" => "资产归还",
            "material_transfer" => "测试料件流转",
            _ => bizType
        };

    private static List<string> ValidateWorkflowBpmnForStatus(string? bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            return new List<string>();
        }

        var errors = BpmnValidator.ValidateSecurity(bpmnXml);
        errors.AddRange(BpmnValidator.Validate(bpmnXml));
        if (errors.Count > 0)
        {
            return errors;
        }

        try
        {
            var process = BpmnParser.Parse(bpmnXml);
            return BpmnParser.Validate(process);
        }
        catch (Exception ex)
        {
            return new List<string> { $"BPMN XML 解析失败: {ex.Message}" };
        }
    }

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
