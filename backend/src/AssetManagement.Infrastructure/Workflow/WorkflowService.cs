using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
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
        await using var tx = await _db.Database.BeginTransactionAsync();
        var workflow = new WorkflowEntity();
        await ApplyWorkflowDefinition(workflow, request);

        _db.Workflows.Add(workflow);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            throw new BizException(4094, "业务类型已有启用流程");
        }
        await tx.CommitAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task<WorkflowDto> SaveWorkflowAsync(int id, UpdateWorkflowMetadataRequest request)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var workflow = await LockWorkflowAsync(id);
        var requestedBizType = request.BizType?.Trim() ?? "";
        var bizTypeChanges = !string.Equals(workflow.BizType, requestedBizType, StringComparison.Ordinal);
        var hasPendingInstances = await _db.ApprovalFlows.AnyAsync(x => x.WorkflowId == id && x.Status == "pending")
                                  || await _db.MaterialFlows.AnyAsync(x => x.WorkflowId == id && x.Status == "pending");
        if (bizTypeChanges && hasPendingInstances)
        {
            throw new BizException(4093, "该流程仍有进行中的实例，不能修改业务类型");
        }
        await ApplyWorkflowMetadata(workflow, request.Name, request.BizType);
        _db.Entry(workflow).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task<WorkflowDto> SaveWorkflowDesignAsync(int id, DesignWorkflowRequest request)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var workflow = await LockWorkflowAsync(id);
        ValidateBpmnXml(request.BpmnXml);
        if (string.Equals(workflow.BpmnXml, request.BpmnXml, StringComparison.Ordinal))
        {
            await tx.CommitAsync();
            return ToWorkflowDto(workflow);
        }

        var hasPendingInstances = await _db.ApprovalFlows.AnyAsync(x => x.WorkflowId == id && x.Status == "pending")
                                  || await _db.MaterialFlows.AnyAsync(x => x.WorkflowId == id && x.Status == "pending");
        if (!hasPendingInstances)
        {
            workflow.BpmnXml = request.BpmnXml;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ToWorkflowDto(workflow);
        }

        var originalName = workflow.Name;
        var originalActive = workflow.IsActive;
        workflow.IsActive = false;
        workflow.Name = HistoricalWorkflowName(workflow);
        await _db.SaveChangesAsync();
        var nextVersion = new WorkflowEntity
        {
            Name = originalName,
            BizType = workflow.BizType,
            BpmnXml = request.BpmnXml,
            IsActive = originalActive
        };
        _db.Workflows.Add(nextVersion);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ToWorkflowDto(nextVersion);
    }

    private static string HistoricalWorkflowName(WorkflowEntity workflow)
    {
        var suffix = $"（历史版本 {workflow.Id}）";
        var maxPrefixLength = Math.Max(0, 100 - suffix.Length);
        var prefix = workflow.Name.Length > maxPrefixLength ? workflow.Name[..maxPrefixLength] : workflow.Name;
        return prefix + suffix;
    }

    public async Task<WorkflowDto> SetWorkflowStatusAsync(int id, bool isActive)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var workflow = await LockWorkflowAsync(id);
        if (isActive)
        {
            await EnsureNoOtherActiveWorkflowForBizType(workflow.BizType, workflow.Id);
        }
        workflow.IsActive = isActive;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ToWorkflowDto(workflow);
    }

    public async Task DeleteWorkflowAsync(int id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var workflow = await LockWorkflowAsync(id);
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
        await tx.CommitAsync();
    }

    public async Task<ApprovalFlowDto> StartAsync(StartApprovalRequest request, int applicantId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var workflow = await _db.Workflows
            .FromSqlInterpolated($"SELECT * FROM workflows WHERE BizType = {request.BizType} AND IsActive = 1 FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync();
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

        var asset = await _db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AssetId)
            ?? throw new BizException(4048, "资产不存在");
        if (asset.IsDeleted)
        {
            throw new BizException(4048, "资产不存在");
        }

        var applicant = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == applicantId && x.IsActive)
            ?? throw new BizException(4041, "用户不存在或已停用");
        await EnsureAssetInScopeAsync(asset, applicant);

        ValidateAssetCanStartFlow(asset, workflow.BizType, applicantId);
        if (workflow.BizType == "transfer" && !request.TransfereeId.HasValue)
            throw new BizException(4001, "转让申请必须选择接收人");
        if (workflow.BizType == "transfer" && request.TransfereeId == applicantId)
            throw new BizException(4001, "接收人不能与申请人相同");
        var returnDate = ValidateReturnDate(workflow.BizType, request.ReturnDate);
        DateOnly? originalReturnDate = null;
        var transferee = request.TransfereeId.HasValue
            ? await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TransfereeId.Value && x.IsActive)
            : null;
        if (request.TransfereeId.HasValue && transferee is null)
            throw new BizException(4041, "接收人不存在或已停用");

        // 解析 BPMN 流程定义
        var bpmnProcess = BpmnParser.Parse(workflow.BpmnXml);

        // 与删除路径使用相同的资产行锁，并在获锁后重新读取/校验。
        // 否则“发起时已读取”与“删除时尚无 pending”可能同时成立。
        var lockedAsset = await _db.Assets
            .FromSqlInterpolated($"SELECT * FROM assets WHERE Id = {request.AssetId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync()
            ?? throw new BizException(4048, "资产不存在");
        if (lockedAsset.IsDeleted)
        {
            throw new BizException(4048, "资产不存在");
        }
        await EnsureAssetInScopeAsync(lockedAsset, applicant);
        ValidateAssetCanStartFlow(lockedAsset, workflow.BizType, applicantId);
        asset = lockedAsset;

        // 防重检查放事务内，防止并发请求同时通过检查后各自插入
        var activeFlow = await _db.ApprovalFlows
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.Status == "pending");
        if (activeFlow is not null)
            throw new BizException(4056, BuildActiveFlowConflictMessage(activeFlow));

        // 转让只变更借用责任人，不重新计算借用期限。应归还日期始终取自
        // 当前未结束的原借用单，并复制到转让单中作为不可变的业务快照。
        if (workflow.BizType == "transfer" && asset.Status == AssetStatus.Borrowed)
        {
            returnDate = await _db.ApprovalFlows.AsNoTracking()
                .Where(candidate => candidate.AssetId == asset.Id &&
                                    candidate.BizType == "borrow" &&
                                    candidate.Status == "approved" &&
                                    candidate.ConfirmedAt == null &&
                                    candidate.ReturnDate != null)
                .OrderByDescending(candidate => candidate.ApplyTime)
                .Select(candidate => candidate.ReturnDate)
                .FirstOrDefaultAsync();
        }

        if (workflow.BizType == "extension")
        {
            originalReturnDate = await _db.ApprovalFlows.AsNoTracking()
                .Where(candidate => candidate.AssetId == asset.Id &&
                                    candidate.BizType == "borrow" &&
                                    candidate.Status == "approved" &&
                                    candidate.ConfirmedAt == null &&
                                    candidate.ReturnDate != null)
                .OrderByDescending(candidate => candidate.ApplyTime)
                .Select(candidate => candidate.ReturnDate)
                .FirstOrDefaultAsync();
            if (originalReturnDate is null)
                throw new BizException(4055, "未找到当前有效借用记录，无法申请延期");
            if (returnDate <= originalReturnDate)
                throw new BizException(4001, "新归还日期必须晚于原应归还日期");
        }

        // 与用户停用路径共用 users 行锁，并保持 asset -> user 的锁顺序。
        // 只锁实际会被停用校验保护的当事人：借用申请人或转让接收人。
        // 避免同一请求同时持有多个用户锁，与管理员保底锁形成环路。
        var protectedParticipantIds = workflow.BizType switch
        {
            "borrow" => new[] { applicantId },
            "transfer" when request.TransfereeId.HasValue => new[] { request.TransfereeId.Value },
            "extension" => new[] { applicantId },
            _ => Array.Empty<int>()
        };
        var lockedParticipants = await LockActiveParticipantsAsync(protectedParticipantIds);
        if (request.TransfereeId.HasValue)
            transferee = lockedParticipants[request.TransfereeId.Value];

        var flow = new ApprovalFlow
        {
            FlowNo = $"APV-{BusinessClock.Today:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            BizType = workflow.BizType,
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            ApplicantDept = await DepartmentName(applicant.DepartmentId),
            SourceCustodianId = workflow.BizType == "borrow" ? asset.CustodianId : null,
            TransfereeId = transferee?.Id,
            Transferee = transferee?.Name,
            TransfereeDept = await DepartmentName(transferee?.DepartmentId),
            Reason = request.Reason,
            OriginalReturnDate = originalReturnDate,
            ReturnDate = returnDate,
            Status = "pending",
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(2),
            Context = await BuildWorkflowContext(applicant, bpmnProcess)
        };

        // 启动 BPMN 流程引擎
        ExecuteBpmn(() => BpmnEngine.Start(flow, bpmnProcess));
        await NormalizeSignStatesAsync(flow, bpmnProcess);
        await EnsureCurrentApproversResolvableAsync(flow, bpmnProcess);
        flow.ActiveScopeKey = flow.Status == "pending" ? $"asset:{asset.Id}" : null;
        _db.ApprovalFlows.Add(flow);
        if (flow.Status == "approved")
            await _bizEffectApplier.ApplyAsync(flow, applicantId);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "创建审批单发生唯一键冲突，资产 {AssetId}", asset.Id);
            throw new BizException(4056, "该资产已有进行中的审批,请勿重复发起");
        }
        await AddRecord(flow.Id, "start", applicant.Id, applicant.Name, request.Reason);
        await NotifyCurrentApproversAsync(flow, bpmnProcess, $"您有新的待审批任务：{asset.Name} 的{BizTypeLabel(workflow.BizType)}申请");
        await tx.CommitAsync();

        return await ToFlowDtoAsync(flow);
    }

    public async Task<List<ApprovalFlowDto>> PendingAsync(int userId)
        => (await PendingPageAsync(userId, new ApprovalFlowPageQuery { PageSize = AppConstants.MaxPageSize })).Items.ToList();

    public async Task<PagedResult<ApprovalFlowDto>> PendingPageAsync(int userId, ApprovalFlowPageQuery query)
    {
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);
        var user = await LoadUser(userId);

        var baseQuery = ApplyApprovalPageFilters(
                _db.ApprovalFlows.AsNoTracking().Where(x => x.Status == FlowStatus.Pending), query)
            .OrderByDescending(x => x.Id);
        const int scanSize = 200;
        var scanOffset = 0;
        var pageOffset = PageOffset(page, pageSize);
        var actionableTotal = 0;
        var selected = new List<(ApprovalFlow Flow, List<string> Nodes)>();
        var workflowMap = new Dictionary<int, WorkflowEntity>();
        var processCache = new Dictionary<int, BpmnProcess>();
        var approverCache = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        while (true)
        {
            var chunk = await baseQuery.Skip(scanOffset).Take(scanSize).ToListAsync();
            if (chunk.Count == 0) break;
            scanOffset += chunk.Count;
            var workflowIds = chunk.Select(flow => flow.WorkflowId).Distinct()
                .Where(workflowId => !workflowMap.ContainsKey(workflowId)).ToArray();
            var workflows = await _db.Workflows.AsNoTracking()
                .Where(workflow => workflowIds.Contains(workflow.Id)).ToListAsync();
            foreach (var workflow in workflows) workflowMap[workflow.Id] = workflow;
            foreach (var flow in chunk)
            {
                var actionableNodeIds = await GetActionableNodeIdsAsync(
                    flow, user, workflowMap, processCache, approverCache);
                if (actionableNodeIds.Count == 0) continue;
                if ((long)actionableTotal >= pageOffset && selected.Count < pageSize)
                    selected.Add((flow, actionableNodeIds));
                actionableTotal++;
            }
            if (chunk.Count < scanSize) break;
        }
        var pageFlows = selected.Select(item => item.Flow).ToList();
        await HydrateParticipantNamesAsync(pageFlows);
        var renderContext = await BuildProgressRenderContextAsync(pageFlows);
        var items = new List<ApprovalFlowDto>(pageFlows.Count);
        foreach (var item in selected) items.Add(await ToFlowDtoAsync(item.Flow, item.Nodes, renderContext));
        return new PagedResult<ApprovalFlowDto> { Items = items, Total = actionableTotal, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<ApprovalFlowDto>> HandledPageAsync(int userId, ApprovalFlowPageQuery query)
    {
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);
        await LoadUser(userId);
        var handledFlowIds = _db.FlowRecords.AsNoTracking()
            .Where(record => record.OperatorUserId == userId &&
                             (record.Action == "approve" || record.Action == "reject"))
            .Select(record => record.FlowId)
            .Distinct();
        var flowsQuery = ApplyApprovalPageFilters(
            _db.ApprovalFlows.AsNoTracking().Where(flow => handledFlowIds.Contains(flow.Id)), query);
        var total = await flowsQuery.CountAsync();
        var pageOffset = PageOffset(page, pageSize);
        if (pageOffset >= total)
            return new PagedResult<ApprovalFlowDto> { Total = total, Page = page, PageSize = pageSize };

        var flows = await flowsQuery.OrderByDescending(flow => flow.Id)
            .Skip((int)pageOffset).Take(pageSize).ToListAsync();
        await HydrateParticipantNamesAsync(flows);
        var flowIds = flows.Select(flow => flow.Id).ToArray();
        var latestRecords = (await _db.FlowRecords.AsNoTracking()
                .Where(record => flowIds.Contains(record.FlowId) &&
                                 record.OperatorUserId == userId &&
                                 (record.Action == "approve" || record.Action == "reject"))
                .OrderByDescending(record => record.OperatedAt)
                .ThenByDescending(record => record.Id)
                .ToListAsync())
            .GroupBy(record => record.FlowId)
            .ToDictionary(group => group.Key, group => group.First());
        var renderContext = await BuildProgressRenderContextAsync(flows);
        var items = new List<ApprovalFlowDto>(flows.Count);
        foreach (var flow in flows)
        {
            var dto = await ToFlowDtoAsync(flow, renderContext: renderContext);
            var record = latestRecords[flow.Id];
            items.Add(dto with
            {
                MyApprovalAction = record.Action,
                MyApprovalNodeId = record.NodeId,
                MyApprovalTime = record.OperatedAt
            });
        }
        return new PagedResult<ApprovalFlowDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<List<ApprovalFlowDto>> MineAsync(int userId)
        => (await MinePageAsync(userId, new ApprovalFlowPageQuery { PageSize = AppConstants.MaxPageSize })).Items.ToList();

    public async Task<PagedResult<ApprovalFlowDto>> MinePageAsync(int userId, ApprovalFlowPageQuery query)
    {
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);
        var flowsQuery = ApplyApprovalPageFilters(
            _db.ApprovalFlows.AsNoTracking().Where(x => x.ApplicantId == userId), query);
        var total = await flowsQuery.CountAsync();
        var pageOffset = PageOffset(page, pageSize);
        if (pageOffset >= total)
            return new PagedResult<ApprovalFlowDto> { Total = total, Page = page, PageSize = pageSize };
        var flows = await flowsQuery.OrderByDescending(x => x.Id)
            .Skip((int)pageOffset).Take(pageSize).ToListAsync();
        await HydrateParticipantNamesAsync(flows);

        var renderContext = await BuildProgressRenderContextAsync(flows);
        var result = new List<ApprovalFlowDto>();
        foreach (var flow in flows) result.Add(await ToFlowDtoAsync(flow, renderContext: renderContext));
        return new PagedResult<ApprovalFlowDto> { Items = result, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<List<ApprovalFlowDto>> PendingReturnsAsync(int userId)
        => (await PendingReturnsPageAsync(userId, new ApprovalFlowPageQuery { PageSize = AppConstants.MaxPageSize })).Items.ToList();

    public async Task<PagedResult<ApprovalFlowDto>> PendingReturnsPageAsync(int userId, ApprovalFlowPageQuery pageQuery)
    {
        var (page, pageSize) = NormalizePage(pageQuery.Page, pageQuery.PageSize);
        var user = await LoadUser(userId);
        var managedDepartmentIds = await ManagedDepartmentIdsAsync(user.Id);
        if (managedDepartmentIds.Length == 0)
            return new PagedResult<ApprovalFlowDto> { Page = page, PageSize = pageSize };

        var query = ApplyApprovalPageFilters(_db.ApprovalFlows.AsNoTracking()
            .Where(x => x.Status == "approved" && x.BizType == "borrow" && x.ConfirmedAt == null)
            .Where(x => _db.Assets.Any(a =>
                a.Id == x.AssetId &&
                a.DepartmentId.HasValue &&
                managedDepartmentIds.Contains(a.DepartmentId.Value))), pageQuery with { Keyword = null });
        if (!string.IsNullOrWhiteSpace(pageQuery.Keyword))
        {
            var keyword = pageQuery.Keyword.Trim();
            query = query.Where(flow => flow.FlowNo.Contains(keyword) ||
                                        flow.AssetNo.Contains(keyword) ||
                                        flow.AssetName.Contains(keyword) ||
                                        _db.Assets.Any(asset => asset.Id == flow.AssetId &&
                                            asset.CustodianId.HasValue &&
                                            _db.Users.Any(current => current.Id == asset.CustodianId.Value &&
                                                (current.Name.Contains(keyword) || current.EmployeeNo.Contains(keyword)))));
        }
        var total = await query.CountAsync();
        var pageOffset = PageOffset(page, pageSize);
        if (pageOffset >= total)
            return new PagedResult<ApprovalFlowDto> { Total = total, Page = page, PageSize = pageSize };
        var flows = await query.OrderByDescending(x => x.Id)
            .Skip((int)pageOffset).Take(pageSize).ToListAsync();
        await HydrateParticipantNamesAsync(flows);

        var renderContext = await BuildProgressRenderContextAsync(flows);
        var result = new List<ApprovalFlowDto>();
        var currentCustodians = await CurrentCustodiansByAssetAsync(flows.Select(flow => flow.AssetId));
        foreach (var flow in flows)
        {
            var dto = await ToFlowDtoAsync(flow, renderContext: renderContext);
            if (currentCustodians.TryGetValue(flow.AssetId, out var custodian))
            {
                dto = dto with
                {
                    Applicant = custodian.Name,
                    ApplicantDept = custodian.DepartmentName
                };
            }
            result.Add(dto);
        }
        return new PagedResult<ApprovalFlowDto> { Items = result, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<ApprovalFlowDto> GetFlowAsync(int id, int userId)
    {
        var flow = await LoadFlow(id);
        var user = await LoadUser(userId);
        await EnsureCanViewFlowAsync(flow, user);
        return await ToFlowDtoAsync(flow, await GetActionableNodeIdsAsync(flow, user));
    }

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

        await NormalizeSignStatesAsync(flow, bpmnProcess);
        await ReconcileInactiveSignersAsync(flow, nodeId);
        ExecuteBpmn(() => BpmnEngine.Approve(flow, bpmnProcess, nodeId, ApprovalIdentity(flow, nodeId, user), request.Opinion));

        if (flow.Status == "pending")
        {
            await NormalizeSignStatesAsync(flow, bpmnProcess);
            await EnsureCurrentApproversResolvableAsync(flow, bpmnProcess);
        }

        // 检查流程是否完成
        if (flow.Status == "approved")
        {
            flow.ActiveScopeKey = null;
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
        await AddRecord(id, "approve", user.Id, user.Name, $"节点 {nodeId}: {request.Opinion}", nodeId);

        // 流程完成 → 通知申请人；未完成 → 通知下一审批节点的审批人
        if (flow.Status == "approved")
        {
            var notifications = new List<CreateNotificationRequest>
            {
                new()
                {
                    Type = "approval_approved",
                    Title = $"审批通过：{flow.AssetName}",
                    Body = $"您发起的 {flow.AssetName}（{flow.AssetNo}）{BizTypeLabel(flow.BizType)}申请已通过审批。",
                    FlowId = id,
                    UserId = flow.ApplicantId,
                    IdempotencyKey = $"approval_approved_{id}_{flow.ApplicantId}"
                }
            };
            if (flow.BizType == "transfer" && flow.TransfereeId.HasValue && flow.TransfereeId.Value != flow.ApplicantId)
            {
                notifications.Add(new CreateNotificationRequest
                {
                    Type = "transfer_received",
                    Title = $"资产已转让给您：{flow.AssetName}",
                    Body = $"资产 {flow.AssetNo}（{flow.AssetName}）已完成转让审批，当前保管人为您。",
                    FlowId = id,
                    UserId = flow.TransfereeId.Value,
                    IdempotencyKey = $"transfer_received_{id}_{flow.TransfereeId.Value}"
                });
            }
            await _notifications.CreateBatchAsync(notifications);
        }
        else if (flow.Status == "pending")
        {
            await NotifyCurrentApproversAsync(flow, bpmnProcess, $"您有新的待审批任务：{flow.AssetName} 的{BizTypeLabel(flow.BizType)}申请");
        }
        await tx.CommitAsync();

        return await ToFlowDtoAsync(flow);
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
        ExecuteBpmn(() => BpmnEngine.Reject(flow, nodeId, user.Name, request.Reason));
        flow.ActiveScopeKey = null;

        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该审批单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "reject", user.Id, user.Name, request.Reason, nodeId);
        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            Type = "approval_rejected",
            Title = $"审批驳回：{flow.AssetName}",
            Body = $"您发起的 {flow.AssetName}（{flow.AssetNo}）{BizTypeLabel(flow.BizType)}申请被驳回。原因：{request.Reason}",
            FlowId = id,
            UserId = flow.ApplicantId,
            IdempotencyKey = $"approval_rejected_{id}_{flow.ApplicantId}"
        });
        await tx.CommitAsync();

        return await ToFlowDtoAsync(flow);
    }

    /// <summary>
    /// 执行 BPMN 引擎操作，将流程结构性错误（如网关无匹配分支、循环超限）转换为可读的业务异常，
    /// 避免 ExceptionMiddleware 将其吞掉细节后统一返回无意义的 500。
    /// </summary>
    private static void ExecuteBpmn(Action operation)
    {
        try
        {
            operation();
        }
        catch (InvalidOperationException ex)
        {
            throw new BizException(4053, $"审批流程执行失败：{ex.Message}");
        }
    }

    private static DateOnly? ValidateReturnDate(string bizType, string? value)
    {
        if (bizType is not ("borrow" or "extension")) return null;
        if (string.IsNullOrWhiteSpace(value))
            throw new BizException(4001, bizType == "extension"
                ? "延期申请必须选择新的归还日期"
                : "借用申请必须选择归还日期");
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var returnDate))
            throw new BizException(4001, "归还日期格式必须为 yyyy-MM-dd");
        if (returnDate <= BusinessClock.TodayDateOnly)
            throw new BizException(4001, "归还日期必须晚于今天");
        return returnDate;
    }

    private static void ValidateAssetCanStartFlow(Asset asset, string bizType, int applicantId)
    {
        if (bizType == "borrow" && asset.Status != AssetStatus.Available)
            throw new BizException(4055, "资产当前不可用,无法发起借用流程");
        if (bizType == "borrow" && asset.CustodianId == applicantId)
            throw new BizException(4055, "当前保管人不能借用自己保管的资产");
        if (bizType == "transfer" && asset.Status is not (AssetStatus.Available or AssetStatus.Borrowed))
            throw new BizException(4055, "维护或报废状态的资产无法发起转让流程");
        if (bizType == "return" &&
            (asset.Status != AssetStatus.Borrowed || asset.CustodianId != applicantId))
            throw new BizException(4055, "只有当前借用人可以发起归还流程");
        if (bizType == "extension" &&
            (asset.Status != AssetStatus.Borrowed || asset.CustodianId != applicantId))
            throw new BizException(4055, "只有当前借用人可以发起延期流程");
        if (bizType == "transfer" && asset.CustodianId != applicantId)
            throw new BizException(4055, "只有当前保管人可以发起转让流程");
    }

    public async Task<ApprovalFlowDto> WithdrawAsync(int id, int userId)
    {
        var flow = await LoadFlow(id);
        EnsureActive(flow);
        if (flow.ApplicantId != userId)
            throw new BizException(4031, "只有申请人本人可以撤回该申请");

        var applicant = await LoadUser(userId);
        await using var tx = await _db.Database.BeginTransactionAsync();
        ExecuteBpmn(() => BpmnEngine.Withdraw(flow, applicant.Name));
        flow.ActiveScopeKey = null;
        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该审批单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "withdraw", applicant.Id, applicant.Name, "申请人主动撤回");
        await tx.CommitAsync();

        return await ToFlowDtoAsync(flow);
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
        var token = flow.BpmnTokens.GetValueOrDefault(nodeId)
            ?? throw new BizException(4014, "该节点当前不可审批");
        if (token.AddedSigners?.ContainsKey(user.Id.ToString()) == true)
            throw new BizException(4057, "被加签人不能再次发起加签");

        if (string.IsNullOrWhiteSpace(request.Who))
            throw new BizException(4057, "请选择加签人");

        if (!int.TryParse(request.Who, out var signUserId))
            throw new BizException(4001, "加签人标识无效，请重新选择");
        if (signUserId == user.Id)
            throw new BizException(4057, "不能加签自己");
        var signUser = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == signUserId && x.IsActive)
            ?? throw new BizException(4041, "加签人不存在或已停用");
        var isActiveSupervisor = signUser.UserRoles.Any(ur =>
            ur.Role is { Code: "supervisor", IsActive: true });
        var hasActiveDepartment = signUser.DepartmentId.HasValue &&
            await _db.Departments.AsNoTracking().AnyAsync(x =>
                x.Id == signUser.DepartmentId.Value && x.IsActive);
        if (!isActiveSupervisor || !hasActiveDepartment)
            throw new BizException(4057, "加签人必须是有效部门的部门主管");

        token.SignStates ??= new Dictionary<string, bool>
        {
            [user.Id.ToString()] = false
        };
        if (!token.SignStates.TryAdd(signUser.Id.ToString(), false))
            throw new BizException(4094, "该用户已在当前签核名单中");
        token.AddedSigners ??= new Dictionary<string, int>();
        token.AddedSigners[signUser.Id.ToString()] = user.Id;
        var addSignNotificationVersion = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该审批单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "add_sign", user.Id, user.Name, $"节点 {nodeId}: 加签 {signUser.Name}", nodeId);
        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            Type = "approval_pending",
            Title = $"待审批加签：{flow.AssetName}",
            Body = $"{user.Name} 已将您加签到资产 {flow.AssetNo}（{flow.AssetName}）的审批节点，请及时处理。",
            FlowId = flow.Id,
            UserId = signUser.Id,
            IdempotencyKey = NotificationIdempotencyKeys.PendingApproval(
                "approval_pending",
                flow.Id,
                nodeId,
                signUser.Id,
                addSignNotificationVersion),
        });
        await tx.CommitAsync();
        return await ToFlowDtoAsync(flow, await GetActionableNodeIdsAsync(flow, user));
    }

    public async Task<ApprovalFlowDto> CancelAddSignAsync(int id, CancelAddSignRequest request, int userId)
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
        if (!int.TryParse(request.Who, out var signUserId))
            throw new BizException(4001, "请选择要取消的加签人");

        var token = flow.BpmnTokens.GetValueOrDefault(nodeId)
            ?? throw new BizException(4014, "该节点当前不可审批");
        var signKey = signUserId.ToString();
        if (token.AddedSigners is null || !token.AddedSigners.TryGetValue(signKey, out var addedByUserId))
            throw new BizException(4057, "该用户不是动态加签人员，无法取消");
        if (addedByUserId != user.Id)
            throw new BizException(4031, "只有执行加签的人可以取消该加签");
        if (token.SignStates?.GetValueOrDefault(signKey) != false)
            throw new BizException(4057, "该加签人已完成审批，无法取消");

        var signUser = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == signUserId);
        token.SignStates.Remove(signKey);
        token.AddedSigners.Remove(signKey);
        await using var tx = await _db.Database.BeginTransactionAsync();
        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该审批单已被他人处理，请刷新后重试");
        }
        await AddRecord(id, "cancel_add_sign", user.Id, user.Name, $"节点 {nodeId}: 取消加签 {signUser?.Name ?? signKey}", nodeId);
        await tx.CommitAsync();
        return await ToFlowDtoAsync(flow, await GetActionableNodeIdsAsync(flow, user));
    }

    public async Task<ApprovalFlowDto> ConfirmReturnAsync(int id, int userId)
    {
        var flow = await LoadFlow(id);
        if (flow.Status != "approved" || flow.BizType != "borrow")
        {
            throw new BizException(4011, "该工单不可确认接收");
        }

        if (flow.ConfirmedAt.HasValue)
        {
            throw new BizException(4012, "该工单已确认接收");
        }

        var user = await LoadUser(userId);
        var scopedAsset = await _db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.AssetId)
            ?? throw new BizException(4048, "资产不存在");
        var managedDepartmentIds = await ManagedDepartmentIdsAsync(user.Id);
        if (!scopedAsset.DepartmentId.HasValue || !managedDepartmentIds.Contains(scopedAsset.DepartmentId.Value))
            throw new BizException(4030, "仅资产所属组织负责人可确认归还");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var lockedFlow = await _db.ApprovalFlows
            .FromSqlInterpolated($"SELECT * FROM approval_flows WHERE Id = {id} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync();
        if (lockedFlow.Status != "approved" || lockedFlow.BizType != "borrow" || lockedFlow.ConfirmedAt.HasValue)
            throw new BizException(4090, "归还单状态已变化，请刷新后重试");

        var asset = await _db.Assets
            .FromSqlInterpolated($"SELECT * FROM assets WHERE Id = {flow.AssetId} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync();
        var returningCustodianId = asset?.CustodianId;
        if (await _db.ApprovalFlows.AsNoTracking().AnyAsync(candidate =>
                candidate.Id != id && candidate.AssetId == flow.AssetId && candidate.Status == "pending"))
            throw new BizException(4092, "该资产存在进行中的归还或转让审批，不能确认接收");

        flow.ConfirmedAt = DateTime.UtcNow;
        if (asset != null)
        {
            if (asset.IsDeleted || asset.Status != AssetStatus.Borrowed || !asset.CustodianId.HasValue)
                throw new BizException(4090, "资产当前状态与该归还单不一致，请刷新后重试");
            asset.Status = AssetStatus.Available;
            asset.CustodianId = await _bizEffectApplier.ResolveReturnCustodianIdAsync(
                asset, flow.SourceCustodianId, user.Id);
            asset.RowVersion++;
        }
        flow.RowVersion++;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BizException(4090, "操作冲突，该归还单已被处理，请刷新后重试");
        }
        await AddRecord(id, "confirm_return", user.Id, user.Name, "确认归还接收");
        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            Type = "return_confirmed",
            Title = $"归还接收确认：{flow.AssetName}",
            Body = $"您借用的 {flow.AssetName}（{flow.AssetNo}）已确认接收归还。",
            FlowId = id,
            UserId = returningCustodianId ?? flow.ApplicantId,
            IdempotencyKey = $"return_confirmed_{id}_{returningCustodianId ?? flow.ApplicantId}"
        });
        await tx.CommitAsync();

        return await ToFlowDtoAsync(flow);
    }

    // ========== 私有辅助方法 ==========

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
        => (Math.Max(page, 1), Math.Clamp(pageSize, 1, AppConstants.MaxPageSize));

    private static long PageOffset(int page, int pageSize)
        => ((long)page - 1L) * pageSize;

    private static IQueryable<ApprovalFlow> ApplyApprovalPageFilters(
        IQueryable<ApprovalFlow> query,
        ApprovalFlowPageQuery filter)
    {
        if (filter.FlowId.HasValue) query = query.Where(flow => flow.Id == filter.FlowId.Value);
        if (!string.IsNullOrWhiteSpace(filter.BizType))
        {
            var bizType = filter.BizType.Trim();
            query = query.Where(flow => flow.BizType == bizType);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToLowerInvariant();
            query = query.Where(flow => flow.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(filter.ReturnDate))
        {
            var returnDateText = filter.ReturnDate.Trim();
            if (!DateOnly.TryParseExact(returnDateText, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var returnDate))
                throw new BizException(4001, "归还日期格式必须为 yyyy-MM-dd");
            query = query.Where(flow => flow.ReturnDate == returnDate);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(flow => flow.FlowNo.Contains(keyword) || flow.AssetNo.Contains(keyword) ||
                                        flow.AssetName.Contains(keyword) || flow.Applicant.Contains(keyword) ||
                                        (flow.Transferee != null && flow.Transferee.Contains(keyword)));
        }
        return query;
    }

    private async Task<WorkflowEntity> LoadWorkflow(int id)
        => await _db.Workflows.AsTracking().SingleOrDefaultAsync(x => x.Id == id)
           ?? throw new BizException(4049, "流程不存在");

    private async Task<WorkflowEntity> LockWorkflowAsync(int id)
        => await _db.Workflows
               .FromSqlInterpolated($"SELECT * FROM workflows WHERE Id = {id} FOR UPDATE")
               .AsTracking()
               .SingleOrDefaultAsync()
           ?? throw new BizException(4049, "流程不存在");

    private async Task<Dictionary<int, User>> LockActiveParticipantsAsync(IEnumerable<int> userIds)
    {
        var result = new Dictionary<int, User>();
        foreach (var userId in userIds.Distinct().OrderBy(id => id))
        {
            var user = await _db.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE Id = {userId} FOR UPDATE")
                .AsTracking()
                .SingleOrDefaultAsync();
            if (user is null || !user.IsActive)
                throw new BizException(4041, "用户不存在或已停用");
            result[userId] = user;
        }
        return result;
    }

    private async Task ApplyWorkflowDefinition(WorkflowEntity workflow, SaveWorkflowRequest request)
    {
        await ApplyWorkflowMetadata(workflow, request.Name, request.BizType);
        ValidateBpmnXml(request.BpmnXml);
        workflow.BpmnXml = request.BpmnXml;
    }

    private async Task ApplyWorkflowMetadata(WorkflowEntity workflow, string? requestedName, string? requestedBizType)
    {
        var name = requestedName?.Trim() ?? "";
        var bizType = requestedBizType?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BizException(4001, "流程名称不能为空");
        }
        if (name.Length > 100)
        {
            throw new BizException(4001, "流程名称不能超过 100 个字符");
        }

        if (string.IsNullOrWhiteSpace(bizType))
        {
            throw new BizException(4001, "业务类型不能为空");
        }
        if (bizType.Length > 50)
        {
            throw new BizException(4001, "业务类型不能超过 50 个字符");
        }

        if (await _db.Workflows.AnyAsync(x => x.Name == name && x.Id != workflow.Id))
        {
            throw new BizException(4094, "流程名称已存在");
        }

        if (workflow.IsActive)
            await EnsureNoOtherActiveWorkflowForBizType(bizType, workflow.Id);

        workflow.Name = name;
        workflow.BizType = bizType;
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

        if (Encoding.UTF8.GetByteCount(bpmnXml) > 65_535)
        {
            throw new BizException(4001, "BPMN 定义的 UTF-8 内容不能超过 65535 字节");
        }

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

    private void EnsureActive(ApprovalFlow flow)
    {
        if (flow.Status != "pending")
        {
            throw new BizException(4013, "该工单已结束，无法操作");
        }
    }

    private async Task<List<string>> GetActionableNodeIdsAsync(
        ApprovalFlow flow,
        User user,
        Dictionary<int, WorkflowEntity>? workflowMap = null,
        Dictionary<int, BpmnProcess>? processCache = null,
        Dictionary<string, List<int>>? approverCache = null)
    {
        WorkflowEntity? workflow;
        if (workflowMap != null)
            workflowMap.TryGetValue(flow.WorkflowId, out workflow);
        else
            workflow = await _db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.WorkflowId);
        if (string.IsNullOrWhiteSpace(workflow?.BpmnXml)) return new List<string>();

        BpmnProcess process;
        if (processCache is not null && processCache.TryGetValue(workflow.Id, out var cachedProcess))
        {
            process = cachedProcess;
        }
        else
        {
            process = BpmnParser.Parse(workflow.BpmnXml);
            if (processCache is not null) processCache[workflow.Id] = process;
        }
        var result = new List<string>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) || token.Status != BpmnTokenStatus.Active)
                continue;
            var node = process.FindNode(nodeId);
            if (node?.Type == BpmnNodeType.UserTask &&
                await IsApproverForNode(node, user, flow, approverCache))
                result.Add(nodeId);
        }
        return result;
    }

    private async Task<bool> CanApprove(ApprovalFlow flow, User user)
        => (await GetActionableNodeIdsAsync(flow, user)).Count > 0;

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

        if (!await IsApproverForNode(node, user, flow))
        {
            throw new BizException(4016, "您无权审批此节点");
        }
    }

    private async Task<bool> IsApproverForNode(
        BpmnNode node,
        User user,
        ApprovalFlow flow,
        Dictionary<string, List<int>>? approverCache = null)
    {
        if (flow.BpmnTokens.TryGetValue(node.Id, out var token) && token.SignStates is { Count: > 0 })
        {
            if (HasNormalizedSignStates(token))
            {
                return token.SignStates.TryGetValue(user.Id.ToString(), out var signed) && !signed;
            }

            var resolvedIds = await ResolveApproverUserIdsAsync(node, flow);
            return resolvedIds.Contains(user.Id);
        }

        if (approverCache is not null)
        {
            var key = ProgressApproverCacheKey(flow.WorkflowId, node, flow);
            if (!approverCache.TryGetValue(key, out var resolvedIds))
            {
                resolvedIds = await ResolveApproverUserIdsAsync(node, flow);
                approverCache[key] = resolvedIds;
            }
            return resolvedIds.Contains(user.Id);
        }

        // 从节点属性中获取审批人配置
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var candidateUsers = node.Properties.GetValueOrDefault("candidateUsers");
        var candidateGroups = node.Properties.GetValueOrDefault("candidateGroups");

        // 指定用户
        if (!string.IsNullOrEmpty(assignee))
        {
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
            {
                var approverIds = await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(
                    _db, flow.ApplicantId, assignee);
                return approverIds.Contains(user.Id);
            }
            else if (assignee == "deptManager")
            {
                var targetDeptId = await ResolveDeptManagerTargetDepartmentIdAsync(node, flow);
                if (targetDeptId is null)
                {
                    return false;
                }

                var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetDeptId.Value);
                if (user.Id == flow.ApplicantId) return false;
                var isSameDeptAdmin = user.DepartmentId == targetDeptId &&
                                      user.UserRoles.Any(ur => ur.Role is { Code: "supervisor", IsActive: true });
                var isDepartmentManager = department?.ManagerId == user.Id;
                return isSameDeptAdmin || isDepartmentManager;
            }
            else if (assignee == "supervisor")
            {
                var approverIds = await ResolveSupervisorApproverUserIdsAsync(flow);
                return approverIds.Contains(user.Id);
            }
            else
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, assignee);
                return resolution.IsResolved && resolution.UserIds.Contains(user.Id);
            }
        }

        // 候选用户列表
        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var candidateUser in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolution = await BpmnApproverIdentityResolver.ResolveUsersAsync(_db, candidateUser);
                if (resolution.IsResolved && resolution.UserIds.Contains(user.Id)) return true;
            }
        }

        // 候选角色
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

    private bool IsAdmin(User user)
        => user.UserRoles.Any(ur => ur.Role is { Code: "admin", IsActive: true });

    private static bool IsSupervisor(User user)
        => user.UserRoles.Any(ur => ur.Role is { Code: "supervisor", IsActive: true });

    private async Task EnsureAssetInScopeAsync(Asset asset, User user)
    {
        if (IsAdmin(user) || !IsSupervisor(user)) return;
        if (!user.DepartmentId.HasValue)
            throw new BizException(4048, "资产不存在");
        var allowed = await DescendantDepartmentIdsAsync(user.DepartmentId.Value);
        if (!asset.DepartmentId.HasValue || !allowed.Contains(asset.DepartmentId.Value))
            throw new BizException(4048, "资产不存在");
    }

    private async Task EnsureCanViewFlowAsync(ApprovalFlow flow, User user)
    {
        if (IsAdmin(user) || flow.ApplicantId == user.Id || flow.TransfereeId == user.Id)
            return;
        if (await CanApprove(flow, user)) return;
        if (IsSupervisor(user) && user.DepartmentId.HasValue)
        {
            var asset = await _db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.AssetId);
            var allowed = await DescendantDepartmentIdsAsync(user.DepartmentId.Value);
            if (asset?.DepartmentId is int departmentId && allowed.Contains(departmentId)) return;
        }
        throw new BizException(4030, "无权查看该审批单");
    }

    private async Task NormalizeSignStatesAsync(ApprovalFlow flow, BpmnProcess process)
    {
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask ||
                !node.Properties.TryGetValue("approvalMode", out var mode) || mode != "all" ||
                !flow.BpmnTokens.TryGetValue(nodeId, out var token)) continue;
            if (HasNormalizedSignStates(token)) continue;
            var approverIds = await ResolveApproverUserIdsAsync(node, flow);
            if (approverIds.Count == 0)
                throw new BizException(4051, $"会签节点 {node.Name} 未解析到有效审批人");
            token.SignStates = approverIds.Distinct().ToDictionary(x => x.ToString(), _ => false);
        }
    }

    private static bool HasNormalizedSignStates(BpmnToken token)
        => token.SignStates is { Count: > 0 } && token.SignStates.Keys.All(key =>
            int.TryParse(key, out var userId) && userId > 0);

    private async Task ReconcileInactiveSignersAsync(ApprovalFlow flow, string nodeId)
    {
        if (!flow.BpmnTokens.TryGetValue(nodeId, out var token) ||
            token.SignStates is not { Count: > 0 } || !HasNormalizedSignStates(token)) return;

        var pendingIds = token.SignStates
            .Where(item => !item.Value)
            .Select(item => int.Parse(item.Key))
            .ToArray();
        var activeIds = await _db.Users.AsNoTracking()
            .Where(user => user.IsActive && pendingIds.Contains(user.Id))
            .Select(user => user.Id.ToString())
            .ToListAsync();
        var activeSet = activeIds.ToHashSet(StringComparer.Ordinal);
        foreach (var inactiveKey in token.SignStates
                     .Where(item => !item.Value && !activeSet.Contains(item.Key))
                     .Select(item => item.Key)
                     .ToArray())
        {
            token.SignStates.Remove(inactiveKey);
            token.AddedSigners?.Remove(inactiveKey);
        }

        if (!token.SignStates.Values.Any(signed => !signed)) token.SignStates = null;
    }

    private async Task EnsureCurrentApproversResolvableAsync(ApprovalFlow flow, BpmnProcess process)
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

    private static string ApprovalIdentity(ApprovalFlow flow, string nodeId, User user)
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
            if (OrganizationApprovalResolver.IsOrganizationAssignee(assignee))
            {
                result.AddRange(await OrganizationApprovalResolver.ResolveApproverUserIdsAsync(
                    _db, flow.ApplicantId, assignee));
            }
            else if (assignee == "deptManager")
            {
                var targetDeptId = await ResolveDeptManagerTargetDepartmentIdAsync(node, flow);
                if (targetDeptId is not null)
                {
                    var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetDeptId.Value);
                    if (dept?.ManagerId is int managerId && managerId != flow.ApplicantId &&
                        await _db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
                        result.Add(managerId);
                    var deptAdmins = await _db.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .Where(u => u.Id != flow.ApplicantId && u.IsActive && u.DepartmentId == targetDeptId &&
                                    u.UserRoles.Any(ur => ur.Role != null && ur.Role.IsActive && ur.Role.Code == "supervisor"))
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
            else
            {
                await AddExplicitApproverUserIdsAsync(result, assignee);
            }
        }

        if (!string.IsNullOrEmpty(candidateUsers))
        {
            foreach (var part in candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                await AddExplicitApproverUserIdsAsync(result, part.Trim());
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

    private async Task<int[]> DescendantDepartmentIdsAsync(int rootId)
    {
        var all = await _db.Departments.AsNoTracking().Select(x => new { x.Id, x.ParentId }).ToListAsync();
        var ids = new HashSet<int> { rootId };
        void Walk(int parentId)
        {
            foreach (var child in all.Where(x => x.ParentId == parentId))
            {
                if (ids.Add(child.Id))
                {
                    Walk(child.Id);
                }
            }
        }
        Walk(rootId);
        return ids.ToArray();
    }

    private async Task<int[]> ManagedDepartmentIdsAsync(int userId)
    {
        var all = await _db.Departments.AsNoTracking()
            .Select(x => new { x.Id, x.ParentId, x.ManagerId })
            .ToListAsync();
        var roots = all.Where(x => x.ManagerId == userId).Select(x => x.Id).ToArray();
        if (roots.Length == 0) return [];

        var ids = new HashSet<int>(roots);
        var queue = new Queue<int>(roots);
        while (queue.TryDequeue(out var parentId))
        {
            foreach (var childId in all.Where(x => x.ParentId == parentId).Select(x => x.Id))
            {
                if (ids.Add(childId)) queue.Enqueue(childId);
            }
        }
        return ids.ToArray();
    }

    private async Task<List<int>> ResolveSupervisorApproverUserIdsAsync(ApprovalFlow flow)
    {
        var result = new List<int>();
        var applicant = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.ApplicantId);
        if (applicant?.DepartmentId is not null)
        {
            var department = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicant.DepartmentId.Value);
            if (department?.ManagerId is int managerId && managerId != flow.ApplicantId &&
                await _db.Users.AsNoTracking().AnyAsync(x => x.Id == managerId && x.IsActive))
            {
                result.Add(managerId);
            }
        }

        // 兼容旧数据：组织节点未配置负责人时，仍可使用历史维护的直属上级。
        if (result.Count == 0 && applicant?.SupervisorId is int supervisorId && supervisorId != flow.ApplicantId &&
            await _db.Users.AsNoTracking().AnyAsync(x => x.Id == supervisorId && x.IsActive))
        {
            result.Add(supervisorId);
        }

        result.RemoveAll(id => id == flow.ApplicantId);
        return result;
    }

    private async Task AddExplicitApproverUserIdsAsync(List<int> result, string value)
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

    private async Task<string?> DepartmentName(int? deptId)
    {
        if (!deptId.HasValue) return null;
        var dept = await _db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deptId.Value);
        return dept?.Name;
    }

    private async Task HydrateParticipantNamesAsync(IEnumerable<ApprovalFlow> flows)
    {
        var flowList = flows.ToList();
        var userIds = flowList
            .SelectMany(flow => new[] { flow.ApplicantId, flow.TransfereeId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (userIds.Length == 0) return;

        var userNames = await _db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Name);
        foreach (var flow in flowList)
        {
            if (userNames.TryGetValue(flow.ApplicantId, out var applicantName))
                flow.Applicant = applicantName;
            if (flow.TransfereeId is int transfereeId && userNames.TryGetValue(transfereeId, out var transfereeName))
                flow.Transferee = transfereeName;
        }
    }

    private async Task<Dictionary<int, CurrentCustodianDisplay>> CurrentCustodiansByAssetAsync(
        IEnumerable<int> assetIds)
    {
        var ids = assetIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, CurrentCustodianDisplay>();

        var assets = await _db.Assets.AsNoTracking()
            .Where(asset => ids.Contains(asset.Id) && asset.CustodianId.HasValue)
            .Select(asset => new { asset.Id, CustodianId = asset.CustodianId!.Value })
            .ToListAsync();
        var custodianIds = assets.Select(asset => asset.CustodianId).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(user => custodianIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Name, user.DepartmentId })
            .ToListAsync();
        var departmentIds = users.Where(user => user.DepartmentId.HasValue)
            .Select(user => user.DepartmentId!.Value)
            .Distinct()
            .ToArray();
        var departments = await _db.Departments.AsNoTracking()
            .Where(department => departmentIds.Contains(department.Id))
            .ToDictionaryAsync(department => department.Id, department => department.Name);
        var usersById = users.ToDictionary(user => user.Id);

        return assets
            .Where(asset => usersById.ContainsKey(asset.CustodianId))
            .ToDictionary(asset => asset.Id, asset =>
            {
                var user = usersById[asset.CustodianId];
                return new CurrentCustodianDisplay(
                    user.Name,
                    user.DepartmentId.HasValue
                        ? departments.GetValueOrDefault(user.DepartmentId.Value)
                        : null);
            });
    }

    private sealed record CurrentCustodianDisplay(string Name, string? DepartmentName);

    private async Task<Dictionary<string, string>> BuildWorkflowContext(User applicant, BpmnProcess process)
    {
        var roleCodes = applicant.UserRoles
            .Select(x => x.Role?.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .Cast<string>()
            .ToArray();

        var context = new Dictionary<string, string>
        {
            ["applicantRole"] = roleCodes.FirstOrDefault() ?? "",
            ["applicantRoles"] = string.Join(",", roleCodes),
            ["isProjectOwner"] = "false"
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

    private async Task AddRecord(
        int flowId,
        string action,
        int? operatorUserId,
        string actor,
        string? remark,
        string? nodeId = null)
    {
        _db.FlowRecords.Add(new FlowRecord
        {
            FlowId = flowId,
            Action = action,
            OperatorUserId = operatorUserId,
            NodeId = nodeId,
            Operator = actor,
            Comment = Truncate(remark, 500),
            OperatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    internal static string? Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static string? FormatDate(DateOnly? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
            "extension" => "借用延期",
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

    private static string BuildActiveFlowConflictMessage(ApprovalFlow flow)
    {
        var currentNodeNames = flow.CurrentNodeIds
            .Select(nodeId => flow.BpmnTokens.GetValueOrDefault(nodeId)?.NodeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();
        var currentNodeText = currentNodeNames.Count > 0
            ? $"，当前节点：{string.Join("、", currentNodeNames)}"
            : "";

        return $"该资产正在由“{flow.Applicant}”发起{BizTypeLabel(flow.BizType)}申请"
               + $"（流程号：{flow.FlowNo}{currentNodeText}），请等待流程结束后再试";
    }

    private async Task<ProgressRenderContext> BuildProgressRenderContextAsync(IReadOnlyCollection<ApprovalFlow> flows)
    {
        var workflowIds = flows.Select(flow => flow.WorkflowId).Distinct().ToArray();
        var workflows = await _db.Workflows.AsNoTracking()
            .Where(workflow => workflowIds.Contains(workflow.Id))
            .Select(workflow => new { workflow.Id, workflow.BpmnXml })
            .ToListAsync();
        var context = new ProgressRenderContext();
        foreach (var workflow in workflows)
        {
            if (!string.IsNullOrWhiteSpace(workflow.BpmnXml))
                context.Processes[workflow.Id] = BpmnParser.Parse(workflow.BpmnXml);
        }

        var userIds = new HashSet<int>();
        foreach (var flow in flows)
        {
            if (!context.Processes.TryGetValue(flow.WorkflowId, out var process)) continue;
            foreach (var token in flow.BpmnTokens.Values)
            {
                var node = process.FindNode(token.NodeId);
                if (node?.Type != BpmnNodeType.UserTask) continue;
                foreach (var execution in token.History)
                    userIds.UnionWith(GetCompletedStepUserIds(ToHistoricalToken(token, execution)));
                if (token.Status == BpmnTokenStatus.Completed)
                    userIds.UnionWith(GetCompletedStepUserIds(token));
            }

            var pendingNodes = flow.CurrentNodeIds
                .Select(process.FindNode)
                .Where(node => node?.Type == BpmnNodeType.UserTask)
                .Cast<BpmnNode>()
                .Concat(flow.Status == FlowStatus.Pending
                    ? FindNextUserTasks(process, flow.CurrentNodeIds).Select(candidate => candidate.Node)
                    : Enumerable.Empty<BpmnNode>())
                .GroupBy(node => node.Id)
                .Select(group => group.First());
            foreach (var node in pendingNodes)
            {
                var key = ProgressApproverCacheKey(flow.WorkflowId, node, flow);
                if (!context.ResolvedApprovers.TryGetValue(key, out var resolved))
                {
                    resolved = await ResolveApproverUserIdsAsync(node, flow);
                    context.ResolvedApprovers[key] = resolved;
                }
                userIds.UnionWith(resolved);
            }
        }

        context.Users = await _db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new ProgressUser(user.Id, user.EmployeeNo, user.Name))
            .ToDictionaryAsync(user => user.Id);
        return context;
    }

    private static List<int> GetCompletedStepUserIds(BpmnToken? token)
    {
        if (token?.SignStates is { Count: > 0 })
            return token.SignStates.Keys.Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0).Distinct().ToList();
        return ParseCompletedApproverIds(token);
    }

    private sealed class ProgressRenderContext
    {
        public Dictionary<int, BpmnProcess> Processes { get; } = new();
        public Dictionary<string, List<int>> ResolvedApprovers { get; } = new(StringComparer.Ordinal);
        public Dictionary<int, ProgressUser> Users { get; set; } = new();
    }

    private static string ProgressApproverCacheKey(int workflowId, BpmnNode node, ApprovalFlow flow)
    {
        var assignee = node.Properties.GetValueOrDefault("assignee");
        var isDynamic = !string.IsNullOrWhiteSpace(assignee) &&
                        (OrganizationApprovalResolver.IsOrganizationAssignee(assignee)
                         || assignee is "deptManager" or "supervisor");
        return isDynamic
            ? $"{workflowId}|{node.Id}|{flow.BizType}|{flow.ApplicantId}|{flow.TransfereeId}"
            : $"{workflowId}|{node.Id}|static";
    }

    private sealed record ProgressUser(int Id, string EmployeeNo, string Name);

    private async Task<ApprovalFlowDto> ToFlowDtoAsync(
        ApprovalFlow flow,
        IEnumerable<string>? actionableNodeIds = null,
        ProgressRenderContext? renderContext = null)
    {
        var dto = ToFlowDto(flow, actionableNodeIds);
        BpmnProcess? process;
        if (renderContext is null)
        {
            var workflow = await _db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == flow.WorkflowId);
            if (string.IsNullOrWhiteSpace(workflow?.BpmnXml)) return dto;
            process = BpmnParser.Parse(workflow.BpmnXml);
        }
        else
        {
            process = renderContext.Processes.GetValueOrDefault(flow.WorkflowId);
            if (process is null) return dto;
        }
        var completed = new List<WorkflowProgressStepDto>();
        foreach (var token in flow.BpmnTokens.Values.OrderBy(x => x.CompletedAt ?? x.StartedAt))
        {
            var node = process.FindNode(token.NodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;
            foreach (var execution in token.History.OrderBy(x => x.CompletedAt))
                completed.Add(await BuildProgressStepAsync(node, flow, ToHistoricalToken(token, execution), "completed", false, renderContext));
            if (token.Status == BpmnTokenStatus.Completed)
                completed.Add(await BuildProgressStepAsync(node, flow, token, "completed", false, renderContext));
        }

        var current = new List<WorkflowProgressStepDto>();
        foreach (var nodeId in flow.CurrentNodeIds)
        {
            var node = process.FindNode(nodeId);
            if (node?.Type != BpmnNodeType.UserTask) continue;
            flow.BpmnTokens.TryGetValue(nodeId, out var token);
            current.Add(await BuildProgressStepAsync(node, flow, token, "current", false, renderContext));
        }

        var next = new List<WorkflowProgressStepDto>();
        if (flow.Status == "pending")
        {
            foreach (var candidate in FindNextUserTasks(process, flow.CurrentNodeIds))
            {
                next.Add(await BuildProgressStepAsync(candidate.Node, flow, null, "next", candidate.IsPossible, renderContext));
            }
        }

        return dto with
        {
            ProgressSteps = completed.Concat(current).Concat(next).ToList(),
            CurrentSteps = current,
            NextSteps = next
        };
    }

    private static BpmnToken ToHistoricalToken(BpmnToken current, BpmnTokenExecution execution) => new()
    {
        NodeId = current.NodeId,
        NodeName = current.NodeName,
        Status = BpmnTokenStatus.Completed,
        StartedAt = execution.StartedAt,
        Approver = execution.Approver,
        Opinion = execution.Opinion,
        CompletedAt = execution.CompletedAt,
        SignStates = execution.SignStates
    };

    private async Task<WorkflowProgressStepDto> BuildProgressStepAsync(
        BpmnNode node,
        ApprovalFlow flow,
        BpmnToken? token,
        string state,
        bool isPossible,
        ProgressRenderContext? renderContext = null)
    {
        var userIds = state == "completed"
            ? ParseCompletedApproverIds(token)
            : renderContext?.ResolvedApprovers.GetValueOrDefault(ProgressApproverCacheKey(flow.WorkflowId, node, flow))
              ?? await ResolveApproverUserIdsAsync(node, flow);
        if (token?.SignStates is { Count: > 0 })
        {
            userIds = token.SignStates.Keys
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();
        }

        List<ProgressUser> users;
        if (renderContext is null)
        {
            users = await _db.Users.AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .OrderBy(x => x.Name)
                .Select(x => new ProgressUser(x.Id, x.EmployeeNo, x.Name))
                .ToListAsync();
        }
        else
        {
            users = userIds.Select(id => renderContext.Users.GetValueOrDefault(id))
                .Where(user => user is not null)
                .Cast<ProgressUser>()
                .OrderBy(user => user.Name)
                .ToList();
        }
        var assignees = users.Select(user => new WorkflowAssigneeDto
        {
            UserId = user.Id,
            EmployeeNo = user.EmployeeNo,
            Name = user.Name,
            Status = ResolveAssigneeStatus(token, user.Id, user.Name, state)
        }).ToList();

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
            Assignees = assignees
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

    private static ApprovalFlowDto ToFlowDto(ApprovalFlow f, IEnumerable<string>? actionableNodeIds = null) => new()
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
        OriginalReturnDate = FormatDate(f.OriginalReturnDate),
        ReturnDate = FormatDate(f.ReturnDate),
        Status = f.Status,
        CurrentNodeIds = f.CurrentNodeIds,
        ActionableNodeIds = actionableNodeIds?.ToList() ?? new List<string>(),
        BpmnTokens = f.BpmnTokens,
        ApplyTime = f.ApplyTime,
        Deadline = f.Deadline,
        ConfirmedAt = f.ConfirmedAt
    };
}
