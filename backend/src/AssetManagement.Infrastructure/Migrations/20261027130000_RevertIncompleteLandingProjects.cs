using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

/// <summary>
/// 历史数据里，未删除、未结案、进度为落地跟进，且仍有未删除阶段不满足进入落地条件的项目，一次性改回测试中。
/// 判定与 <c>StageBlocksLanding</c> 相同：没有结束日期，或测试内容、测试结果为空白。不改表结构。Down 不回滚数据。
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261027130000_RevertIncompleteLandingProjects")]
public partial class RevertIncompleteLandingProjects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `test_projects` AS p
            SET p.`ProgressCode` = 'testing',
                p.`RowVersion` = p.`RowVersion` + 1
            WHERE p.`IsDeleted` = 0
              AND p.`ClosedDate` IS NULL
              AND LOWER(p.`ProgressCode`) = 'landing'
              AND EXISTS (
                SELECT 1
                FROM `project_test_stages` AS s
                WHERE s.`ProjectId` = p.`Id`
                  AND s.`IsDeleted` = 0
                  AND (
                    s.`EndDate` IS NULL
                    OR s.`TestContent` IS NULL
                    OR TRIM(REPLACE(REPLACE(REPLACE(s.`TestContent`, CHAR(9), ' '), CHAR(10), ' '), CHAR(13), ' ')) = ''
                    OR s.`TestResult` IS NULL
                    OR TRIM(REPLACE(REPLACE(REPLACE(s.`TestResult`, CHAR(9), ' '), CHAR(10), ' '), CHAR(13), ' ')) = ''
                  )
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
