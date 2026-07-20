using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

/// <inheritdoc />
public partial class HardenWorkflowAndTransferConcurrency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // MySQL 5.7 DDL 会隐式提交，先检查历史冲突，避免部分建列后才在唯一索引处失败。
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `assert_workflow_transfer_concurrency`");
        migrationBuilder.Sql("""
            CREATE PROCEDURE `assert_workflow_transfer_concurrency`()
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM `approval_flows` WHERE `Status` = 'pending'
                    GROUP BY `AssetId` HAVING COUNT(*) > 1
                ) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate pending approval flow';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM `material_flows` WHERE `Status` = 'pending'
                    GROUP BY `MaterialId` HAVING COUNT(*) > 1
                ) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate pending material flow';
                END IF;
            END
            """);
        migrationBuilder.Sql("CALL `assert_workflow_transfer_concurrency`()");
        migrationBuilder.Sql("DROP PROCEDURE `assert_workflow_transfer_concurrency`");

        // 信息架构检查使这个 Up 在 MySQL DDL 中途失败后可安全重跑。
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `apply_workflow_transfer_columns`");
        migrationBuilder.Sql("""
            CREATE PROCEDURE `apply_workflow_transfer_columns`()
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'test_materials' AND COLUMN_NAME = 'RowVersion') THEN
                    ALTER TABLE `test_materials` ADD COLUMN `RowVersion` int unsigned NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND COLUMN_NAME = 'ActiveScopeKey') THEN
                    ALTER TABLE `material_flows` ADD COLUMN `ActiveScopeKey` varchar(100) CHARACTER SET utf8mb4 NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'assets' AND COLUMN_NAME = 'RowVersion') THEN
                    ALTER TABLE `assets` ADD COLUMN `RowVersion` int unsigned NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'approval_flows' AND COLUMN_NAME = 'ActiveScopeKey') THEN
                    ALTER TABLE `approval_flows` ADD COLUMN `ActiveScopeKey` varchar(100) CHARACTER SET utf8mb4 NULL;
                END IF;
            END
            """);
        migrationBuilder.Sql("CALL `apply_workflow_transfer_columns`()");
        migrationBuilder.Sql("DROP PROCEDURE `apply_workflow_transfer_columns`");

        // 历史进行中实例必须先纳入唯一锁。若历史库已存在同一资产/料件的重复
        // pending 数据，随后创建唯一索引会明确失败，要求先清理冲突而不是静默放行。
        migrationBuilder.Sql("UPDATE `approval_flows` SET `ActiveScopeKey` = CONCAT('asset:', `AssetId`) WHERE `Status` = 'pending' AND `ActiveScopeKey` IS NULL;");
        migrationBuilder.Sql("UPDATE `material_flows` SET `ActiveScopeKey` = CONCAT('material:', `MaterialId`) WHERE `Status` = 'pending' AND `ActiveScopeKey` IS NULL;");

        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `apply_workflow_transfer_indexes`");
        migrationBuilder.Sql("""
            CREATE PROCEDURE `apply_workflow_transfer_indexes`()
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND INDEX_NAME = 'IX_material_flows_ActiveScopeKey') THEN
                    CREATE UNIQUE INDEX `IX_material_flows_ActiveScopeKey` ON `material_flows` (`ActiveScopeKey`);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'approval_flows' AND INDEX_NAME = 'IX_approval_flows_ActiveScopeKey') THEN
                    CREATE UNIQUE INDEX `IX_approval_flows_ActiveScopeKey` ON `approval_flows` (`ActiveScopeKey`);
                END IF;
            END
            """);
        migrationBuilder.Sql("CALL `apply_workflow_transfer_indexes`()");
        migrationBuilder.Sql("DROP PROCEDURE `apply_workflow_transfer_indexes`");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_material_flows_ActiveScopeKey",
            table: "material_flows");

        migrationBuilder.DropIndex(
            name: "IX_approval_flows_ActiveScopeKey",
            table: "approval_flows");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "test_materials");

        migrationBuilder.DropColumn(
            name: "ActiveScopeKey",
            table: "material_flows");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "assets");

        migrationBuilder.DropColumn(
            name: "ActiveScopeKey",
            table: "approval_flows");
    }
}
