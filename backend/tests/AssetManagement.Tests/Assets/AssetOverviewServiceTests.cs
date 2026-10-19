using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Assets;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Assets;

public class AssetOverviewServiceTests : MySqlFixtureBase
{
    [Fact]
    public async Task Overview_groups_by_real_second_level_ancestry_and_preserves_contract_totals()
    {
        var rd = Category("RD", "RD");
        var ops = Category("OPS", "OPS");
        var deletedRoot = Category("OLD", "OLD", isDeleted: true);
        _db.AssetCategories.AddRange(rd, ops, deletedRoot);
        await _db.SaveChangesAsync();

        var rdPlc = Category("PLC", "RD-PLC", rd.Id, remark: "研发控制器");
        var opsPlc = Category("PLC", "OPS-PLC", ops.Id, remark: "运维控制器");
        var empty = Category("EMPTY", "RD-EMPTY", rd.Id);
        var deletedSecond = Category("DEL", "RD-DEL", rd.Id, isDeleted: true);
        var belowDeletedRoot = Category("PLC", "OLD-PLC", deletedRoot.Id);
        _db.AssetCategories.AddRange(rdPlc, opsPlc, empty, deletedSecond, belowDeletedRoot);
        await _db.SaveChangesAsync();

        var rdSensor = Category("SENSOR", "RD-PLC-SENSOR", rdPlc.Id);
        _db.AssetCategories.Add(rdSensor);
        await _db.SaveChangesAsync();
        var rdDeepLeaf = Category("AI", "RD-PLC-SENSOR-AI", rdSensor.Id);
        _db.AssetCategories.Add(rdDeepLeaf);
        await _db.SaveChangesAsync();

        _db.Assets.AddRange(
            Asset("OV-001", rdPlc.Id, AssetStatus.Available, quantity: 99),
            Asset("OV-002", rdSensor.Id, AssetStatus.Borrowed, quantity: 42),
            Asset("OV-003", rdDeepLeaf.Id, AssetStatus.Available),
            Asset("OV-004", opsPlc.Id, AssetStatus.Borrowed),
            Asset("OV-005", rd.Id, AssetStatus.Available),
            Asset("OV-006", deletedSecond.Id, AssetStatus.Borrowed),
            Asset("OV-007", belowDeletedRoot.Id, AssetStatus.Available),
            Asset("OV-DELETED", rdPlc.Id, AssetStatus.Borrowed, isDeleted: true));
        await _db.SaveChangesAsync();

        var commands = new List<string>();
        var commandCounter = new DbCommandCounterInterceptor
        {
            ReaderExecutingObserver = command => commands.Add(command.CommandText)
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(commandCounter)
            .Options;
        await using var queryDb = new AppDbContext(options);

        var result = await new AssetOverviewService(queryDb).GetOverviewAsync();

        result.Total.Should().Be(7, "统计应按资产记录数计算且排除已删除资产，不能累加 Quantity");
        result.Available.Should().Be(4);
        result.Borrowed.Should().Be(3);
        (result.Available + result.Borrowed).Should().Be(result.Total);
        result.UncategorizedTotal.Should().Be(3,
            "一级分类资产、已删除分类资产及已删除祖先下的资产均不能归入二级分类");
        (result.Categories.Sum(category => category.Total) + result.UncategorizedTotal)
            .Should().Be(result.Total);

        result.Categories.Should().HaveCount(3,
            "未删除的有效二级分类即使没有资产也应返回，删除分支不能返回");
        result.Categories.Should().ContainSingle(category =>
            category.CategoryId == empty.Id
            && category.Total == 0
            && category.Available == 0
            && category.Borrowed == 0);

        var rdRow = result.Categories.Single(category => category.CategoryId == rdPlc.Id);
        rdRow.CategoryCode.Should().Be("RD-PLC");
        rdRow.Label.Should().Be("PLC");
        rdRow.ParentCode.Should().Be("RD");
        rdRow.Remark.Should().Be("研发控制器");
        rdRow.Total.Should().Be(3);
        rdRow.Available.Should().Be(2);
        rdRow.Borrowed.Should().Be(1);

        var opsRow = result.Categories.Single(category => category.CategoryId == opsPlc.Id);
        opsRow.CategoryCode.Should().Be("OPS-PLC");
        opsRow.Label.Should().Be("PLC");
        opsRow.ParentCode.Should().Be("OPS");
        opsRow.Total.Should().Be(1,
            "不同一级分类下同名的二级分类不能合并");
        opsRow.Available.Should().Be(0);
        opsRow.Borrowed.Should().Be(1);

        commandCounter.ReaderCount.Should().Be(2,
            "服务应只读取一次分类元数据和一次数据库聚合结果");
        var assetQuery = commands.Single(command => command.Contains("FROM `assets`", StringComparison.Ordinal));
        assetQuery.Should().Contain("GROUP BY");
        assetQuery.Should().NotContain("AssetNo");
        assetQuery.Should().NotContain("Quantity");
    }

    private static AssetCategory Category(
        string codeSeg,
        string code,
        int? parentId = null,
        string? remark = null,
        bool isDeleted = false)
        => new()
        {
            CodeSeg = codeSeg,
            Code = code,
            ParentId = parentId,
            Remark = remark,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };

    private static Asset Asset(
        string assetNo,
        int categoryId,
        AssetStatus status,
        int quantity = 1,
        bool isDeleted = false)
        => new()
        {
            AssetNo = assetNo,
            Name = assetNo,
            CategoryId = categoryId,
            Quantity = quantity,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
}
