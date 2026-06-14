using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Workflow;

public class BizEffectApplier : IBizEffectApplier
{
    private readonly AppDbContext _db;

    public BizEffectApplier(AppDbContext db)
    {
        _db = db;
    }

    public async Task ApplyAsync(ApprovalFlow flow)
    {
        var asset = await _db.Assets.FindAsync(flow.AssetId);
        if (asset is null)
        {
            return;
        }

        switch (flow.BizType)
        {
            case "borrow":
                asset.Status = AssetStatus.Borrowed;
                asset.CustodianId = flow.ApplicantId;
                break;
            case "transfer":
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

        await _db.SaveChangesAsync();
    }
}
