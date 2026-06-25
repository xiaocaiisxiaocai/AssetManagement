using AssetManagement.Application.Reports;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Reports;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Reports;

public class ReportServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"DataSource=file:report_test_{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new ReportService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
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
    public async Task QueryOverdue_returns_only_overdue_assets()
    {
        // Arrange
        var category = new AssetCategory { CodeSeg = "PC", Code = "PC", ParentId = null };
        _db.AssetCategories.Add(category);
        await _db.SaveChangesAsync();

        var borrowedAsset = new Asset { AssetNo = "PC-001", Name = "逾期电脑", CategoryId = category.Id, Status = AssetStatus.Borrowed, CreatedAt = DateTime.UtcNow };
        var normalAsset = new Asset { AssetNo = "PC-002", Name = "正常电脑", CategoryId = category.Id, Status = AssetStatus.Available, CreatedAt = DateTime.UtcNow };
        _db.Assets.AddRange(borrowedAsset, normalAsset);
        await _db.SaveChangesAsync();

        // 创建逾期流程（预计归还日期是昨天）
        var overdueFlow = new ApprovalFlow
        {
            FlowNo = "F001",
            BizType = "borrow",
            Status = "approved",
            AssetId = borrowedAsset.Id,
            AssetNo = borrowedAsset.AssetNo,
            AssetName = borrowedAsset.Name,
            ApplicantId = 1,
            Applicant = "张三",
            ApplicantDept = "IT部",
            ReturnDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
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
        overdueList[0].Borrower.Should().Be("张三");
        overdueList[0].OverdueDays.Should().BeGreaterThan(0);
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

        // 创建严重逾期流程（预计归还日期是15天前）
        var seriousOverdueFlow = new ApprovalFlow
        {
            FlowNo = "F002",
            BizType = "borrow",
            Status = "approved",
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = 1,
            Applicant = "李四",
            ReturnDate = DateTime.UtcNow.AddDays(-15).ToString("yyyy-MM-dd"),
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

        _db.ApprovalFlows.AddRange(
            new ApprovalFlow
            {
                FlowNo = "F001",
                BizType = "borrow",
                Status = "approved",
                AssetId = borrowedAsset.Id,
                AssetNo = borrowedAsset.AssetNo,
                AssetName = borrowedAsset.Name,
                ApplicantId = 1,
                Applicant = "张三",
                ReturnDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
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
                ApplicantId = 2,
                Applicant = "李四",
                ReturnDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
                ApplyTime = DateTime.UtcNow.AddDays(-2),
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
}
