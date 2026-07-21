using AssetManagement.Application.Reports;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Reports;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AssetManagement.Tests.Reports;

public class ReportServiceTests : MySqlFixtureBase
{
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _service = new ReportService(_db, new NotificationService(_db), new HttpContextAccessor());
    }

    [Fact]
    public async Task GetSummary_empty_database_returns_zero_counts()
    {
        // Act
        var summary = await _service.GetSummaryAsync();

        // Assert
        summary.Total.Should().Be(0);
        summary.Available.Should().Be(0);
        summary.Borrowed.Should().Be(0);
        summary.ByCategory.Should().BeEmpty();
        summary.ByDept.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummary_returns_correct_total_count()
    {
        // Arrange
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        _db.Assets.AddRange(
            new Asset { AssetNo = "PC-001", Name = "电脑1", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-002", Name = "电脑2", CategoryId = category.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-003", Name = "电脑3", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var summary = await _service.GetSummaryAsync();

        // Assert
        summary.Total.Should().Be(3);
        summary.Available.Should().Be(2);
        summary.Borrowed.Should().Be(1);
    }

    [Fact]
    public async Task GetSummary_groups_by_category_correctly()
    {
        // Arrange
        var pc = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        var printer = new AssetCategory { CodeSeg = "PRT", Code = "PRT", ParentId = null };
        _db.AssetCategories.AddRange(pc, printer);
        await _db.SaveChangesAsync();

        _db.Assets.AddRange(
            new Asset { AssetNo = "PC-001", Name = "电脑1", CategoryId = pc.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-002", Name = "电脑2", CategoryId = pc.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PRT-001", Name = "打印机1", CategoryId = printer.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var summary = await _service.GetSummaryAsync();

        // Assert
        summary.ByCategory.Should().HaveCount(2);

        var pcRow = summary.ByCategory.First(x => x.CategoryCode == "PC");
        pcRow.Total.Should().Be(2);
        pcRow.Available.Should().Be(1);
        pcRow.Borrowed.Should().Be(1);
        pcRow.Percent.Should().Be(66.67m);

        var printerRow = summary.ByCategory.First(x => x.CategoryCode == "PRT");
        printerRow.Total.Should().Be(1);
        printerRow.Available.Should().Be(1);
        printerRow.Borrowed.Should().Be(0);
        printerRow.Percent.Should().Be(33.33m);
    }

    [Fact]
    public async Task GetSummary_groups_by_department_correctly()
    {
        // Arrange
        var itDept = new Department { Name = "IT部", Code = "D0001", IsActive = true };
        var hrDept = new Department { Name = "人事部", Code = "D0002", IsActive = true };
        _db.Departments.AddRange(itDept, hrDept);
        await _db.SaveChangesAsync();

        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        _db.Assets.AddRange(
            new Asset { AssetNo = "PC-001", Name = "电脑1", CategoryId = category.Id, DepartmentId = itDept.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-002", Name = "电脑2", CategoryId = category.Id, DepartmentId = itDept.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-003", Name = "电脑3", CategoryId = category.Id, DepartmentId = hrDept.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var summary = await _service.GetSummaryAsync();

        // Assert
        summary.ByDept.Should().HaveCount(2);

        var itRow = summary.ByDept.First(x => x.DepartmentName == "IT部");
        itRow.Total.Should().Be(2);
        itRow.Available.Should().Be(1);
        itRow.Borrowed.Should().Be(1);

        var hrRow = summary.ByDept.First(x => x.DepartmentName == "人事部");
        hrRow.Total.Should().Be(1);
        hrRow.Available.Should().Be(1);
        hrRow.Borrowed.Should().Be(0);
    }

    [Fact]
    public async Task Department_admin_summary_is_limited_to_own_department_and_descendants()
    {
        var root = new Department { Name = "研发部", Code = "D1000", IsActive = true };
        var other = new Department { Name = "财务部", Code = "D2000", IsActive = true };
        _db.Departments.AddRange(root, other);
        await _db.SaveChangesAsync();
        var child = new Department { Name = "研发一组", Code = "D1001", ParentId = root.Id, IsActive = false };
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AddRange(child, category);
        await _db.SaveChangesAsync();
        _db.Assets.AddRange(
            new Asset { AssetNo = "PC-SCOPE-1", Name = "本部门", CategoryId = category.Id, DepartmentId = root.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-SCOPE-2", Name = "子部门", CategoryId = category.Id, DepartmentId = child.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow },
            new Asset { AssetNo = "PC-SCOPE-3", Name = "其他部门", CategoryId = category.Id, DepartmentId = other.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var service = CreateServiceFor(new Claim(ClaimTypes.Role, "supervisor"), new Claim("departmentId", root.Id.ToString()));
        var summary = await service.GetSummaryAsync();

        summary.Total.Should().Be(2);
        summary.ByDept.Select(x => x.DepartmentName).Should().BeEquivalentTo("研发部", "研发一组");
    }

    [Fact]
    public async Task Department_admin_without_department_claim_sees_no_report_data()
    {
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Assets.Add(new Asset { AssetNo = "PC-CLOSED-1", Name = "不可见资产", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var summary = await CreateServiceFor(new Claim(ClaimTypes.Role, "supervisor")).GetSummaryAsync();

        summary.Total.Should().Be(0);
    }

    [Fact]
    public async Task QueryOverdue_returns_only_overdue_assets()
    {
        // Arrange
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        var currentCustodian = new User
        {
            EmployeeNo = $"CURRENT-{Guid.NewGuid():N}"[..20],
            Name = "转让后的当前借用人",
            PasswordHash = "not-used",
            IsActive = true
        };
        _db.Users.Add(currentCustodian);
        await _db.SaveChangesAsync();

        var borrowedAsset = new Asset { AssetNo = "PC-001", Name = "逾期电脑", CategoryId = category.Id, Status = AssetStatus.Borrowed, CustodianId = currentCustodian.Id, CreatedAt = DateTime.UtcNow };
        var normalAsset = new Asset { AssetNo = "PC-002", Name = "正常电脑", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow };
        _db.Assets.AddRange(borrowedAsset, normalAsset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("overdue");

        // 创建逾期流程（预计归还日期是昨天）
        var overdueFlow = new ApprovalFlow
        {
            FlowNo = "F001",
            BizType = "borrow",
            Status = "approved",
            AssetId = borrowedAsset.Id,
            AssetNo = borrowedAsset.AssetNo,
            AssetName = borrowedAsset.Name,
            ApplicantId = applicantId,
            WorkflowId = workflowId,
            Applicant = "张三",
            ApplicantDept = "IT部",
            ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            ApplyTime = DateTime.UtcNow.AddDays(-10),
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        };
        _db.ApprovalFlows.Add(overdueFlow);
        await _db.SaveChangesAsync();

        // Act
        var overdueList = await _service.QueryOverdueAsync();

        // Assert
        overdueList.Should().HaveCount(1);
        overdueList[0].AssetNo.Should().Be("PC-001");
        overdueList[0].Borrower.Should().Be(currentCustodian.Name,
            "转让后的逾期责任人应为资产当前保管人");
        overdueList[0].BorrowerId.Should().Be(currentCustodian.Id);
        overdueList[0].OverdueDays.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QueryOverdue_does_not_apply_export_limit_before_due_date_filtering()
    {
        var category = new AssetCategory { CodeSeg = "CAP", Code = "CAP" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = "CAP-001",
            Name = "未来到期资产",
            CategoryId = category.Id,
            Status = AssetStatus.Borrowed,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("overdue-cap");
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        _db.ApprovalFlows.AddRange(Enumerable.Range(1, 10_001).Select(index => new ApprovalFlow
        {
            FlowNo = $"CAP-{index:00000}",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = applicantId,
            WorkflowId = workflowId,
            Applicant = "未来借用人",
            ReturnDate = futureDate,
            ApplyTime = DateTime.UtcNow,
            Deadline = DateTime.UtcNow.AddDays(1),
        }));
        await _db.SaveChangesAsync();

        var overdue = await _service.QueryOverdueAsync();

        overdue.Should().BeEmpty();
    }

    [Fact]
    public async Task RemindOverdueBatch_prevalidates_entire_batch_before_writing()
    {
        var category = new AssetCategory { CodeSeg = "BAT", Code = "BAT" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = "BAT-001",
            Name = "批量催办资产",
            CategoryId = category.Id,
            Status = AssetStatus.Borrowed,
            CreatedAt = DateTime.UtcNow
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("batch");
        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "BAT-FLOW-001",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = applicantId,
            WorkflowId = workflowId,
            Applicant = "借用人",
            ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            ApplyTime = DateTime.UtcNow.AddDays(-5),
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        });
        await _db.SaveChangesAsync();

        var action = () => _service.RemindOverdueBatchAsync(new[] { asset.Id, int.MaxValue }, 1);

        await action.Should().ThrowAsync<AssetManagement.Application.Common.BizException>();
        (await _db.AuditLogs.CountAsync(x => x.ActionType == "remind")).Should().Be(0);
        (await _db.Notifications.CountAsync()).Should().Be(0);
    }

    private ReportService CreateServiceFor(params Claim[] claims)
    {
        var identityClaims = new[] { new Claim(ClaimTypes.NameIdentifier, "999") }.Concat(claims);
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(identityClaims, "test"))
            }
        };
        return new ReportService(_db, new NotificationService(_db), accessor);
    }

    [Fact]
    public async Task QueryOverdue_marks_serious_when_overdue_more_than_10_days()
    {
        // Arrange
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        var asset = new Asset { AssetNo = "PC-001", Name = "严重逾期电脑", CategoryId = category.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("serious");

        // 创建严重逾期流程（预计归还日期是15天前）
        var seriousOverdueFlow = new ApprovalFlow
        {
            FlowNo = "F002",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = applicantId,
            WorkflowId = workflowId,
            Applicant = "李四",
            ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)),
            ApplyTime = DateTime.UtcNow.AddDays(-20),
            CurrentNodeIds = new List<string>(),
            BpmnTokens = new Dictionary<string, BpmnToken>()
        };
        _db.ApprovalFlows.Add(seriousOverdueFlow);
        await _db.SaveChangesAsync();

        // Act
        var overdueList = await _service.QueryOverdueAsync();

        // Assert
        overdueList.Should().HaveCount(1);
        overdueList[0].IsSerious.Should().BeTrue();
        overdueList[0].OverdueDays.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task QueryBorrowed_filters_by_borrow_status()
    {
        // Arrange
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        var borrowedAsset = new Asset { AssetNo = "PC-001", Name = "借出电脑", CategoryId = category.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow };
        var returnedAsset = new Asset { AssetNo = "PC-002", Name = "已归还电脑", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow };
        _db.Assets.AddRange(borrowedAsset, returnedAsset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("borrowed");

        _db.ApprovalFlows.AddRange(
            new ApprovalFlow
            {
                FlowNo = "F001",
                BizType = "borrow",
                Status = "approved",
                AssetId = borrowedAsset.Id,
                AssetNo = borrowedAsset.AssetNo,
                AssetName = borrowedAsset.Name,
                ApplicantId = applicantId,
                WorkflowId = workflowId,
                Applicant = "张三",
                ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                ApplyTime = DateTime.UtcNow.AddDays(-1),
                CurrentNodeIds = new List<string>(),
                BpmnTokens = new Dictionary<string, BpmnToken>()
            },
            new ApprovalFlow
            {
                FlowNo = "F002",
                BizType = "borrow",
                Status = "approved",
                AssetId = returnedAsset.Id,
                AssetNo = returnedAsset.AssetNo,
                AssetName = returnedAsset.Name,
                ApplicantId = applicantId,
                WorkflowId = workflowId,
                Applicant = "李四",
                ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                ApplyTime = DateTime.UtcNow.AddDays(-2),
                ConfirmedAt = DateTime.UtcNow.AddDays(-1),
                CurrentNodeIds = new List<string>(),
                BpmnTokens = new Dictionary<string, BpmnToken>()
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var query = new BorrowReportQuery { Page = 1, PageSize = 10, Status = "borrowed" };
        var result = await _service.QueryBorrowedAsync(query);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].AssetNo.Should().Be("PC-001");
        result.Items[0].Status.Should().Be("borrowed");
    }

    [Fact]
    public async Task Borrow_history_status_comes_from_each_flow_not_current_asset_status()
    {
        var category = new AssetCategory { CodeSeg = "HIS", Code = $"HIS-{Guid.NewGuid():N}" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = $"HIS-{Guid.NewGuid():N}", Name = "重复借用资产", CategoryId = category.Id,
            Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow,
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("history");
        _db.ApprovalFlows.AddRange(
            new ApprovalFlow
            {
                FlowNo = $"HIS-OLD-{Guid.NewGuid():N}", BizType = "borrow", Status = "approved",
                WorkflowId = workflowId, AssetId = asset.Id, AssetNo = asset.AssetNo, AssetName = asset.Name,
                ApplicantId = applicantId, Applicant = "借用人", ApplyTime = DateTime.UtcNow.AddDays(-20),
                Deadline = DateTime.UtcNow.AddDays(-19), ConfirmedAt = DateTime.UtcNow.AddDays(-10),
            },
            new ApprovalFlow
            {
                FlowNo = $"HIS-NOW-{Guid.NewGuid():N}", BizType = "borrow", Status = "approved",
                WorkflowId = workflowId, AssetId = asset.Id, AssetNo = asset.AssetNo, AssetName = asset.Name,
                ApplicantId = applicantId, Applicant = "借用人", ApplyTime = DateTime.UtcNow.AddDays(-2),
                Deadline = DateTime.UtcNow.AddDays(-1), ConfirmedAt = null,
            });
        await _db.SaveChangesAsync();

        var result = await _service.QueryBorrowedAsync(new BorrowReportQuery { Page = 1, PageSize = 10 });

        result.Items.Should().Contain(x => x.FlowNo.StartsWith("HIS-OLD") && x.Status == "returned");
        result.Items.Should().Contain(x => x.FlowNo.StartsWith("HIS-NOW") && x.Status == "borrowed");
    }

    [Fact]
    public async Task Repeated_overdue_reminder_returns_actual_insert_count()
    {
        var category = new AssetCategory { CodeSeg = "REM", Code = $"REM-{Guid.NewGuid():N}" };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = $"REM-{Guid.NewGuid():N}", Name = "催办计数资产", CategoryId = category.Id,
            Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow,
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        var (applicantId, workflowId) = await CreateFlowReferencesAsync("reminder-count");
        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = $"REM-{Guid.NewGuid():N}", BizType = "borrow", Status = "approved",
            WorkflowId = workflowId, AssetId = asset.Id, AssetNo = asset.AssetNo, AssetName = asset.Name,
            ApplicantId = applicantId, Applicant = "借用人", ApplyTime = DateTime.UtcNow.AddDays(-5),
            Deadline = DateTime.UtcNow.AddDays(-4), ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
        });
        await _db.SaveChangesAsync();

        var first = await _service.RemindOverdueAsync(asset.Id, null);
        var second = await _service.RemindOverdueAsync(asset.Id, null);

        first.Should().Be(1);
        second.Should().Be(0);
        (await _db.Notifications.CountAsync(x => x.UserId == applicantId)).Should().Be(1);
    }

    [Fact]
    public async Task ExportSummary_returns_xlsx_bytes()
    {
        // Arrange
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        _db.Assets.Add(new Asset { AssetNo = "PC-001", Name = "电脑", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // Act
        var bytes = await _service.ExportSummaryAsync();

        // Assert
        bytes.Should().NotBeEmpty();
        // XLSX 文件签名：PK (0x50 0x4B)
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    private async Task<(int UserId, int WorkflowId)> CreateFlowReferencesAsync(string suffix)
    {
        var unique = Guid.NewGuid().ToString("N");
        var user = new User
        {
            EmployeeNo = $"report-{suffix}-{unique}"[..Math.Min(50, $"report-{suffix}-{unique}".Length)],
            Name = $"报表用户-{suffix}",
            PasswordHash = "not-used",
            IsActive = true,
        };
        var workflow = new AssetManagement.Domain.Entities.Workflow
        {
            Name = $"报表流程-{suffix}-{unique}",
            BizType = "borrow",
            IsActive = true,
        };
        _db.AddRange(user, workflow);
        await _db.SaveChangesAsync();
        return (user.Id, workflow.Id);
    }
}
