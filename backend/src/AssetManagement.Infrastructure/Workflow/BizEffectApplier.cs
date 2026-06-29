using System.Text.Encodings.Web;
using System.Text.Json;
using AssetManagement.Application.Workflow;
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
        if (asset is null || asset.IsDeleted)
        {
            return;
        }

        var before = Snapshot(asset);

        switch (flow.BizType)
        {
            case "borrow":
                asset.Status = AssetStatus.Borrowed;
                asset.CustodianId = flow.ApplicantId;
                break;
            case "transfer":
                asset.Status = AssetStatus.Available;
                asset.CustodianId = flow.TransfereeId;
                if (flow.TransfereeId.HasValue)
                {
                    var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == flow.TransfereeId.Value);
                    asset.DepartmentId = user?.DepartmentId;
                }
                break;
            case "return":
                asset.Status = AssetStatus.Available;
                asset.CustodianId = null;
                break;
        }

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
                Before = before,
                After = after,
                Changes = BuildChanges(before, after)
            }, JsonOptions),
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
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
