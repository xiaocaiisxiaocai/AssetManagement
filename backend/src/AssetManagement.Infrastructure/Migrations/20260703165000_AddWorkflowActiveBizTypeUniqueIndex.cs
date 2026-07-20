using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowActiveBizTypeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `apply_workflow_active_unique`");
            migrationBuilder.Sql("""
                CREATE PROCEDURE `apply_workflow_active_unique`()
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM `workflows` WHERE `IsActive` = 1
                        GROUP BY `BizType` HAVING COUNT(*) > 1
                    ) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate active workflow BizType';
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'workflows' AND COLUMN_NAME = 'ActiveBizType'
                    ) THEN
                        ALTER TABLE `workflows` ADD COLUMN `ActiveBizType` varchar(50)
                            GENERATED ALWAYS AS (CASE WHEN `IsActive` THEN `BizType` ELSE NULL END) STORED;
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'workflows'
                          AND INDEX_NAME = 'IX_workflows_ActiveBizType'
                    ) THEN
                        CREATE UNIQUE INDEX `IX_workflows_ActiveBizType` ON `workflows` (`ActiveBizType`);
                    END IF;
                END
                """);
            migrationBuilder.Sql("CALL `apply_workflow_active_unique`()");
            migrationBuilder.Sql("DROP PROCEDURE `apply_workflow_active_unique`");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workflows_ActiveBizType",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "ActiveBizType",
                table: "workflows");
        }
    }
}
