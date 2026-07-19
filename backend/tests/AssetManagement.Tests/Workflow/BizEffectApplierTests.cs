using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Application.Common;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Workflow;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Workflow;

public class BizEffectApplierTests : MySqlFixtureBase
{
    private readonly BizEffectApplier _applier;

    public BizEffectApplierTests()
    {
        _applier = new BizEffectApplier(_db);
    }

    [Fact]
    public async Task ApplyAsync_records_asset_business_audit_log()
    {
        var category = new AssetCategory { CodeSeg = "AUD", Code = "AUD" };
        var asset = new Asset
        {
            AssetNo = "AUD-001",
            Name = "审批审计资产",
            CategoryId = 1,
            Status = AssetStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        asset.CategoryId = category.Id;
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var flow = new ApprovalFlow
        {
            FlowNo = "APV-AUDIT-001",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = 11,
            Applicant = "申请人",
            ApplyTime = DateTime.UtcNow,
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        };

        await _applier.ApplyAsync(flow, operatorUserId: 22);

        var log = await _db.AuditLogs.SingleAsync(x => x.TargetType == "Asset" && x.TargetId == asset.Id.ToString());
        log.UserId.Should().Be(22);
        log.ActionType.Should().Be("business");
        log.Summary.Should().Contain("审批生效");
        log.Detail.Should().Contain("\"flowId\"");
        log.Detail.Should().Contain("\"before\"");
        log.Detail.Should().Contain("\"after\"");
        log.Detail.Should().Contain("\"Status\"");
        log.Detail.Should().Contain("Available");
        log.Detail.Should().Contain("Borrowed");
    }

    [Fact]
    public async Task ApplyAsync_rejects_deleted_asset_instead_of_silently_approving()
    {
        var category = new AssetCategory { CodeSeg = "DEL", Code = "DEL" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = "DEL-001",
            Name = "已删除审批资产",
            CategoryId = category.Id,
            Status = AssetStatus.Available,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var flow = new ApprovalFlow
        {
            FlowNo = "APV-DELETED-001",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = 11,
            Applicant = "申请人",
            ApplyTime = DateTime.UtcNow,
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        };

        var action = () => _applier.ApplyAsync(flow);

        var error = await action.Should().ThrowAsync<BizException>();
        error.Which.Code.Should().Be(4094);
        (await _db.AuditLogs.AnyAsync(x => x.TargetId == asset.Id.ToString())).Should().BeFalse();
    }

}
