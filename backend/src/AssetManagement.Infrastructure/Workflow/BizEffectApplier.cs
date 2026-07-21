using System.Text.Encodings.Web;
using System.Text.Json;
using AssetManagement.Application.Workflow;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

public class BizEffectApplier : IBizEffectApplier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppDbContext _db;

    public BizEffectApplier(AppDbContext db)
    {
        _db = db;
    }

    public async Task ApplyAsync(ApprovalFlow flow, int? operatorUserId = null)
    {
        var asset = await _db.Assets.AsTracking().SingleOrDefaultAsync(x => x.Id == flow.AssetId);
        if (asset is null)
        {
            throw new BizException(4048, "审批关联的资产不存在，业务效果无法生效");
        }
        if (asset.IsDeleted)
        {
            throw new BizException(4094, "审批关联的资产已删除，业务效果无法生效");
        }

        var before = Snapshot(asset);

        switch (flow.BizType)
        {
            case "borrow":
                if (asset.Status != AssetStatus.Available)
                    throw new BizException(4090, "资产状态已变化，无法完成借用审批");
                if (asset.CustodianId != flow.SourceCustodianId)
                    throw new BizException(4090, "资产借出前保管人已变化，请撤回后重新发起");
                await LockActiveUserAsync(flow.ApplicantId, "借用申请人不存在或已停用，请撤回后重新发起");
                asset.Status = AssetStatus.Borrowed;
                asset.CustodianId = flow.ApplicantId;
                break;
            case "transfer":
                if (asset.Status is not (AssetStatus.Available or AssetStatus.Borrowed) ||
                    asset.CustodianId != flow.ApplicantId)
                    throw new BizException(4090, "资产状态或保管人已变化，无法完成转让审批");
                if (!flow.TransfereeId.HasValue)
                    throw new BizException(4001, "转让申请缺少接收人");
                var user = await LockActiveUserAsync(flow.TransfereeId.Value, "接收人不存在或已停用，请撤回后重新发起");
                asset.CustodianId = flow.TransfereeId;
                asset.DepartmentId = user.DepartmentId;
                break;
            case "return":
                if (asset.Status != AssetStatus.Borrowed || asset.CustodianId != flow.ApplicantId)
                    throw new BizException(4090, "资产状态或保管人已变化，无法完成归还审批");
                var borrowFlow = await _db.ApprovalFlows.AsTracking()
                    .Where(candidate => candidate.Id != flow.Id &&
                                        candidate.AssetId == flow.AssetId &&
                                        candidate.BizType == "borrow" &&
                                        candidate.Status == "approved" &&
                                        candidate.ConfirmedAt == null)
                    .OrderByDescending(candidate => candidate.ApplyTime)
                    .FirstOrDefaultAsync();
                asset.CustodianId = await ResolveReturnCustodianIdAsync(
                    asset, borrowFlow?.SourceCustodianId, operatorUserId);
                asset.Status = AssetStatus.Available;
                if (borrowFlow is not null)
                {
                    borrowFlow.ConfirmedAt = DateTime.UtcNow;
                    borrowFlow.RowVersion++;
                }
                break;
            case "extension":
                if (asset.Status != AssetStatus.Borrowed || asset.CustodianId != flow.ApplicantId)
                    throw new BizException(4090, "资产状态或当前借用人已变化，无法完成延期审批");
                await LockActiveUserAsync(flow.ApplicantId, "延期申请人不存在或已停用，请撤回后重新发起");
                if (!flow.OriginalReturnDate.HasValue || !flow.ReturnDate.HasValue)
                    throw new BizException(4001, "延期申请缺少原归还日期或新归还日期");
                if (flow.ReturnDate.Value <= flow.OriginalReturnDate.Value)
                    throw new BizException(4001, "延期申请的新归还日期必须晚于原应归还日期");
                var activeBorrow = await _db.ApprovalFlows.AsTracking()
                    .Where(candidate => candidate.Id != flow.Id &&
                                        candidate.AssetId == flow.AssetId &&
                                        candidate.BizType == "borrow" &&
                                        candidate.Status == "approved" &&
                                        candidate.ConfirmedAt == null)
                    .OrderByDescending(candidate => candidate.ApplyTime)
                    .FirstOrDefaultAsync()
                    ?? throw new BizException(4090, "当前有效借用记录已不存在，无法完成延期审批");
                if (activeBorrow.ReturnDate != flow.OriginalReturnDate)
                    throw new BizException(4090, "原借用期限已变化，请撤回后重新发起延期申请");
                activeBorrow.ReturnDate = flow.ReturnDate;
                activeBorrow.RowVersion++;
                break;
        }
        asset.RowVersion++;

        var after = Snapshot(asset);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = operatorUserId,
            ActionType = "business",
            TargetType = "Asset",
            TargetId = asset.Id.ToString(),
            Summary = $"审批生效：{flow.FlowNo} {flow.BizType} {asset.AssetNo}",
            Detail = JsonSerializer.Serialize(new
            {
                FlowId = flow.Id,
                flow.FlowNo,
                flow.BizType,
                flow.OriginalReturnDate,
                flow.ReturnDate,
                Before = before,
                After = after,
                Changes = BuildChanges(before, after)
            }, JsonOptions),
            OccurredAt = DateTime.UtcNow
        });
    }

    public async Task<int> ResolveReturnCustodianIdAsync(
        Asset asset,
        int? sourceCustodianId,
        int? fallbackManagerId)
    {
        if (sourceCustodianId.HasValue)
        {
            var source = await LockUserAsync(sourceCustodianId.Value);
            if (source is { IsActive: true } &&
                (!asset.DepartmentId.HasValue || source.DepartmentId == asset.DepartmentId))
            {
                return source.Id;
            }
        }

        if (!fallbackManagerId.HasValue)
            throw new BizException(4090, "借出前保管人不可用，请由资产所属组织负责人确认接收入库");

        var fallback = await LockUserAsync(fallbackManagerId.Value);
        if (fallback is not { IsActive: true })
            throw new BizException(4090, "接收入库负责人不存在或已停用");
        if (asset.DepartmentId.HasValue &&
            !await ManagesDepartmentAsync(fallback.Id, asset.DepartmentId.Value))
        {
            throw new BizException(4030, "借出前保管人不可用，仅资产所属组织负责人可接收入库");
        }
        return fallback.Id;
    }

    private async Task<User> LockActiveUserAsync(int userId, string errorMessage)
    {
        var user = await _db.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE Id = {userId} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync();
        if (user is null || !user.IsActive) throw new BizException(4041, errorMessage);
        return user;
    }

    private async Task<User?> LockUserAsync(int userId)
        => await _db.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE Id = {userId} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync();

    private async Task<bool> ManagesDepartmentAsync(int userId, int departmentId)
    {
        var departments = await _db.Departments.AsNoTracking()
            .Select(department => new { department.Id, department.ParentId, department.ManagerId })
            .ToListAsync();
        var byId = departments.ToDictionary(department => department.Id);
        var visited = new HashSet<int>();
        var currentId = departmentId;
        while (visited.Add(currentId) && byId.TryGetValue(currentId, out var department))
        {
            if (department.ManagerId == userId) return true;
            if (!department.ParentId.HasValue) break;
            currentId = department.ParentId.Value;
        }
        return false;
    }

    private static Dictionary<string, object?> Snapshot(Asset asset)
        => new()
        {
            [nameof(Asset.Status)] = asset.Status.ToString(),
            [nameof(Asset.CustodianId)] = asset.CustodianId,
            [nameof(Asset.DepartmentId)] = asset.DepartmentId
        };

    private static List<object> BuildChanges(Dictionary<string, object?> before, Dictionary<string, object?> after)
        => before.Keys
            .Where(key => !Equals(before.GetValueOrDefault(key), after.GetValueOrDefault(key)))
            .OrderBy(key => key)
            .Select(key => new
            {
                Field = key,
                Before = before.GetValueOrDefault(key),
                After = after.GetValueOrDefault(key)
            })
            .Cast<object>()
            .ToList();
}
