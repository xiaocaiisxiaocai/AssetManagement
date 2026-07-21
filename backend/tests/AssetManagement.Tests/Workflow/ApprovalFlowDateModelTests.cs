using AssetManagement.Application.Workflow;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Migrations;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Reports;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Globalization;
using System.Reflection;

namespace AssetManagement.Tests.Workflow;

public class ApprovalFlowDateModelTests
{
    [Fact]
    public void Return_dates_use_date_only_and_mysql_date_while_api_contract_stays_string()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(ApprovalFlow))!;

        entity.FindProperty(nameof(ApprovalFlow.ReturnDate))!.ClrType.Should().Be(typeof(DateOnly?));
        entity.FindProperty(nameof(ApprovalFlow.ReturnDate))!.GetColumnType().Should().Be("date");
        entity.FindProperty(nameof(ApprovalFlow.OriginalReturnDate))!.ClrType.Should().Be(typeof(DateOnly?));
        entity.FindProperty(nameof(ApprovalFlow.OriginalReturnDate))!.GetColumnType().Should().Be("date");
        entity.GetIndexes().Select(index => index.Properties.Select(property => property.Name).ToArray())
            .Should().Contain(properties => properties.SequenceEqual(new[]
            {
                nameof(ApprovalFlow.BizType),
                nameof(ApprovalFlow.Status),
                nameof(ApprovalFlow.ConfirmedAt),
                nameof(ApprovalFlow.ReturnDate)
            }));

        typeof(ApprovalFlowDto).GetProperty(nameof(ApprovalFlowDto.ReturnDate))!.PropertyType
            .Should().Be(typeof(string));
        typeof(ApprovalFlowDto).GetProperty(nameof(ApprovalFlowDto.OriginalReturnDate))!.PropertyType
            .Should().Be(typeof(string));
        db.Database.GetMigrations()
            .Should().Contain("20260721123000_MigrateApprovalReturnDatesToDate");
    }

    [Fact]
    public void Overdue_date_comparison_translates_to_sql()
    {
        using var db = CreateContext();
        var today = new DateOnly(2026, 7, 21);

        var sql = db.ApprovalFlows
            .Where(flow => flow.ReturnDate != null && flow.ReturnDate < today)
            .ToQueryString();

        sql.Should().Contain("ReturnDate");
        sql.Should().Contain("<");
    }

    [Fact]
    public void Return_date_migration_generates_mysql_date_columns_and_overdue_index()
    {
        var migration = new MigrateApprovalReturnDatesToDate();
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });
        using var db = CreateContext();

        var sql = string.Join(Environment.NewLine,
            db.GetService<IMigrationsSqlGenerator>()
                .Generate(builder.Operations, db.Model)
                .Select(command => command.CommandText));

        sql.Should().Contain("MODIFY COLUMN `ReturnDate` date NULL");
        sql.Should().Contain("MODIFY COLUMN `OriginalReturnDate` date NULL");
        sql.Should().Contain("IX_approval_flows_BizType_Status_ConfirmedAt_ReturnDate");
        sql.Should().Contain("SIGNAL SQLSTATE '45000'");
        sql.Should().NotContain("SET ReturnDate = NULL");
        sql.IndexOf("CALL validate_approval_return_dates_20260721()", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("MODIFY COLUMN `ReturnDate` date NULL", StringComparison.Ordinal),
                "改变列类型前必须先无损验证历史数据");

        var downBuilder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { downBuilder });
        var downSql = string.Join(Environment.NewLine,
            db.GetService<IMigrationsSqlGenerator>()
                .Generate(downBuilder.Operations, db.Model)
                .Select(command => command.CommandText));
        downSql.Should().Contain("DATE_FORMAT(STR_TO_DATE(ReturnDate, '%Y-%m-%d'), '%Y-%m-%d')");
        downSql.Should().Contain("DATE_FORMAT(STR_TO_DATE(OriginalReturnDate, '%Y-%m-%d'), '%Y-%m-%d')");
    }

    [Fact]
    public void Report_return_date_contract_is_invariant_under_non_gregorian_server_culture()
    {
        var formatter = typeof(ReportService).GetMethod(
            "FormatDate",
            BindingFlags.Static | BindingFlags.NonPublic);
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            formatter.Should().NotBeNull();
            formatter!.Invoke(null, new object?[] { new DateOnly(2026, 7, 21) })
                .Should().Be("2026-07-21");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                "Server=localhost;Database=approval_date_model_test;User=test;Password=test;",
                new MySqlServerVersion(new Version(5, 7)))
            .Options;
        return new AppDbContext(options);
    }
}
