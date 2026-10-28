using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

/// <summary>
/// 只按已有日期回写阶段状态，不改表结构。Down 不回滚数据。
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261027120000_NormalizeProjectStageStatusFromDates")]
public partial class NormalizeProjectStageStatusFromDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `project_test_stages`
            SET `Status` = 'completed'
            WHERE `EndDate` IS NOT NULL
              AND `Status` <> 'completed';
            """);

        migrationBuilder.Sql("""
            UPDATE `project_test_stages`
            SET `Status` = 'in_progress'
            WHERE `Status` = 'blocked'
              AND `EndDate` IS NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE `project_test_stages`
            SET `Status` = 'planned'
            WHERE `IsDeleted` = 0
              AND `StartDate` IS NULL
              AND `EndDate` IS NULL
              AND `Status` IN ('in_progress', 'blocked');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
