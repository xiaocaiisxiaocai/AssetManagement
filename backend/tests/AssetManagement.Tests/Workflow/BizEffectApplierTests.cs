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
        var applicant = await CreateUserAsync("审批申请人");
        var operatorUser = await CreateUserAsync("审批操作人");
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
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            ApplyTime = DateTime.UtcNow,
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        };

        await _applier.ApplyAsync(flow, operatorUserId: operatorUser.Id);
        await _db.SaveChangesAsync();

        var log = await _db.AuditLogs.SingleAsync(x => x.TargetType == "Asset" && x.TargetId == asset.Id.ToString());
        log.UserId.Should().Be(operatorUser.Id);
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
    public async Task ApplyAsync_leaves_atomic_commit_to_workflow_service()
    {
        var applicant = await CreateUserAsync("原子提交申请人");
        var operatorUser = await CreateUserAsync("原子提交操作人");
        var category = new AssetCategory { CodeSeg = "TXN", Code = "TXN" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = "TXN-001",
            Name = "原子提交资产",
            CategoryId = category.Id,
            Status = AssetStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var flow = new ApprovalFlow
        {
            FlowNo = "APV-TXN-001",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            ApplyTime = DateTime.UtcNow
        };

        await _applier.ApplyAsync(flow, operatorUserId: operatorUser.Id);

        await using var observer = CreateNoTrackingContext();
        (await observer.Assets.SingleAsync(x => x.Id == asset.Id)).Status.Should().Be(AssetStatus.Available);
        (await observer.AuditLogs.AnyAsync(x => x.TargetId == asset.Id.ToString())).Should().BeFalse();

        await _db.SaveChangesAsync();
        (await observer.Assets.SingleAsync(x => x.Id == asset.Id)).Status.Should().Be(AssetStatus.Borrowed);
        (await observer.AuditLogs.AnyAsync(x => x.TargetId == asset.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_rejects_deleted_asset_instead_of_silently_approving()
    {
        var applicant = await CreateUserAsync("已删除资产申请人");
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
            ApplicantId = applicant.Id,
            Applicant = applicant.Name,
            ApplyTime = DateTime.UtcNow,
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        };

        var action = () => _applier.ApplyAsync(flow);

        var error = await action.Should().ThrowAsync<BizException>();
        error.Which.Code.Should().Be(4094);
        (await _db.AuditLogs.AnyAsync(x => x.TargetId == asset.Id.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_rejects_borrow_when_applicant_was_disabled()
    {
        var applicant = await CreateUserAsync("已停用借用人");
        applicant.IsActive = false;
        var category = new AssetCategory { CodeSeg = "OFF", Code = $"OFF-{Guid.NewGuid():N}" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = $"OFF-{Guid.NewGuid():N}", Name = "停用借用测试资产", CategoryId = category.Id,
            Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var flow = new ApprovalFlow
        {
            FlowNo = $"OFF-{Guid.NewGuid():N}", BizType = "borrow", Status = "approved",
            AssetId = asset.Id, AssetNo = asset.AssetNo, AssetName = asset.Name,
            ApplicantId = applicant.Id, Applicant = applicant.Name, ApplyTime = DateTime.UtcNow
        };

        var error = await FluentActions.Invoking(() => _applier.ApplyAsync(flow)).Should().ThrowAsync<BizException>();

        error.Which.Code.Should().Be(4041);
        asset.Status.Should().Be(AssetStatus.Available);
        asset.CustodianId.Should().BeNull();
    }

    [Fact]
    public async Task Return_by_transferee_closes_the_original_borrow_record()
    {
        var sourceCustodian = await CreateUserAsync("借出前保管人");
        var originalBorrower = await CreateUserAsync("原借用人");
        var transferee = await CreateUserAsync("转让接收人");
        var category = new AssetCategory { CodeSeg = "RET", Code = $"RET-{Guid.NewGuid():N}" };
        var workflow = new Domain.Entities.Workflow
        {
            Name = "借用审批",
            BizType = "borrow",
            IsActive = true
        };
        _db.AddRange(category, workflow);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = $"RET-{Guid.NewGuid():N}",
            Name = "已转让借用资产",
            CategoryId = category.Id,
            Status = AssetStatus.Borrowed,
            CustodianId = transferee.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var originalBorrow = new ApprovalFlow
        {
            FlowNo = $"BOR-{Guid.NewGuid():N}",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = originalBorrower.Id,
            Applicant = originalBorrower.Name,
            SourceCustodianId = sourceCustodian.Id,
            ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)).ToString("yyyy-MM-dd"),
            ApplyTime = DateTime.UtcNow.AddDays(-2),
            Deadline = DateTime.UtcNow.AddDays(1)
        };
        _db.ApprovalFlows.Add(originalBorrow);
        await _db.SaveChangesAsync();
        var returnFlow = new ApprovalFlow
        {
            FlowNo = $"RETURN-{Guid.NewGuid():N}",
            BizType = "return",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = transferee.Id,
            Applicant = transferee.Name,
            ApplyTime = DateTime.UtcNow
        };

        await _applier.ApplyAsync(returnFlow, operatorUserId: transferee.Id);
        await _db.SaveChangesAsync();

        asset.Status.Should().Be(AssetStatus.Available);
        asset.CustodianId.Should().Be(sourceCustodian.Id,
            "资产归还后应恢复给借出前的原保管人");
        originalBorrow.ConfirmedAt.Should().NotBeNull(
            "接收人归还后必须关闭保存原应归还日期的借用记录");
    }

    [Fact]
    public async Task Return_without_valid_source_assigns_receiving_department_manager()
    {
        var manager = await CreateUserAsync("接收入库主管");
        var borrower = await CreateUserAsync("无来源借用人");
        var category = new AssetCategory { CodeSeg = "FBK", Code = $"FBK-{Guid.NewGuid():N}" };
        var workflow = new Domain.Entities.Workflow
        {
            Name = "借用审批兜底测试",
            BizType = "borrow",
            IsActive = true
        };
        var department = new Department
        {
            Name = "归还兜底部门",
            Code = $"FBK-{Guid.NewGuid():N}"[..16],
            ManagerId = manager.Id
        };
        _db.AddRange(category, workflow, department);
        await _db.SaveChangesAsync();
        manager.DepartmentId = department.Id;
        borrower.DepartmentId = department.Id;
        var asset = new Asset
        {
            AssetNo = $"FBK-{Guid.NewGuid():N}",
            Name = "无来源保管人归还资产",
            CategoryId = category.Id,
            DepartmentId = department.Id,
            Status = AssetStatus.Borrowed,
            CustodianId = borrower.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var borrowFlow = new ApprovalFlow
        {
            FlowNo = $"BOR-{Guid.NewGuid():N}",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = borrower.Id,
            Applicant = borrower.Name,
            ApplyTime = DateTime.UtcNow.AddDays(-1)
        };
        _db.ApprovalFlows.Add(borrowFlow);
        await _db.SaveChangesAsync();
        var returnFlow = new ApprovalFlow
        {
            FlowNo = $"RETURN-{Guid.NewGuid():N}",
            BizType = "return",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = borrower.Id,
            Applicant = borrower.Name,
            ApplyTime = DateTime.UtcNow
        };

        await _applier.ApplyAsync(returnFlow, operatorUserId: manager.Id);
        await _db.SaveChangesAsync();

        asset.Status.Should().Be(AssetStatus.Available);
        asset.CustodianId.Should().Be(manager.Id,
            "没有有效借出前保管人时，应由实际确认入库的部门主管接管");
    }

    private async Task<User> CreateUserAsync(string name)
    {
        var user = new User
        {
            EmployeeNo = $"BE{Guid.NewGuid():N}"[..12],
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123"),
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

}
