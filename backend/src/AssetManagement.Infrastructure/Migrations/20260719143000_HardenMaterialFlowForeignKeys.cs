using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260719143000_HardenMaterialFlowForeignKeys")]
public partial class HardenMaterialFlowForeignKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // WorkflowId=0 是旧版“直接转移”的伪外键；先检查其他孤儿数据，
        // 再把列改为可空并归一为 NULL，最后创建 Restrict 外键。
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `assert_material_flow_references`");
        migrationBuilder.Sql("""
            CREATE PROCEDURE `assert_material_flow_references`()
            BEGIN
                IF EXISTS (SELECT 1 FROM `material_flows` f LEFT JOIN `test_materials` m ON m.`Id` = f.`MaterialId` WHERE m.`Id` IS NULL) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material flow material';
                END IF;
                IF EXISTS (SELECT 1 FROM `material_flows` f LEFT JOIN `users` u ON u.`Id` = f.`ApplicantId` WHERE u.`Id` IS NULL) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material flow applicant';
                END IF;
                IF EXISTS (SELECT 1 FROM `material_flows` f LEFT JOIN `users` u ON u.`Id` = f.`TransfereeId` WHERE f.`TransfereeId` IS NOT NULL AND u.`Id` IS NULL) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material flow transferee';
                END IF;
                IF EXISTS (SELECT 1 FROM `material_flows` f LEFT JOIN `workflows` w ON w.`Id` = f.`WorkflowId` WHERE f.`WorkflowId` <> 0 AND w.`Id` IS NULL) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material flow workflow';
                END IF;
            END
            """);
        migrationBuilder.Sql("CALL `assert_material_flow_references`()");
        migrationBuilder.Sql("DROP PROCEDURE `assert_material_flow_references`");

        // MySQL 5.7 的 DDL 会隐式提交。逐项检查 information_schema，确保升级在
        // 进程中断、网络断开等造成部分 DDL 已落库但迁移记录未写入时仍可重跑。
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `apply_material_flow_foreign_keys`");
        migrationBuilder.Sql("""
            CREATE PROCEDURE `apply_material_flow_foreign_keys`()
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows'
                      AND COLUMN_NAME = 'WorkflowId' AND IS_NULLABLE = 'NO'
                ) THEN
                    ALTER TABLE `material_flows` MODIFY COLUMN `WorkflowId` int NULL;
                END IF;

                UPDATE `material_flows` SET `WorkflowId` = NULL WHERE `WorkflowId` = 0;

                IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND INDEX_NAME = 'IX_material_flows_TransfereeId') THEN
                    CREATE INDEX `IX_material_flows_TransfereeId` ON `material_flows` (`TransfereeId`);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND INDEX_NAME = 'IX_material_flows_WorkflowId') THEN
                    CREATE INDEX `IX_material_flows_WorkflowId` ON `material_flows` (`WorkflowId`);
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND CONSTRAINT_NAME = 'FK_material_flows_test_materials_MaterialId') THEN
                    ALTER TABLE `material_flows` ADD CONSTRAINT `FK_material_flows_test_materials_MaterialId` FOREIGN KEY (`MaterialId`) REFERENCES `test_materials` (`Id`) ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND CONSTRAINT_NAME = 'FK_material_flows_users_ApplicantId') THEN
                    ALTER TABLE `material_flows` ADD CONSTRAINT `FK_material_flows_users_ApplicantId` FOREIGN KEY (`ApplicantId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND CONSTRAINT_NAME = 'FK_material_flows_users_TransfereeId') THEN
                    ALTER TABLE `material_flows` ADD CONSTRAINT `FK_material_flows_users_TransfereeId` FOREIGN KEY (`TransfereeId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'material_flows' AND CONSTRAINT_NAME = 'FK_material_flows_workflows_WorkflowId') THEN
                    ALTER TABLE `material_flows` ADD CONSTRAINT `FK_material_flows_workflows_WorkflowId` FOREIGN KEY (`WorkflowId`) REFERENCES `workflows` (`Id`) ON DELETE RESTRICT;
                END IF;
            END
            """);
        migrationBuilder.Sql("CALL `apply_material_flow_foreign_keys`()");
        migrationBuilder.Sql("DROP PROCEDURE `apply_material_flow_foreign_keys`");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_material_flows_test_materials_MaterialId", "material_flows");
        migrationBuilder.DropForeignKey("FK_material_flows_users_ApplicantId", "material_flows");
        migrationBuilder.DropForeignKey("FK_material_flows_users_TransfereeId", "material_flows");
        migrationBuilder.DropForeignKey("FK_material_flows_workflows_WorkflowId", "material_flows");
        migrationBuilder.DropIndex("IX_material_flows_TransfereeId", "material_flows");
        migrationBuilder.DropIndex("IX_material_flows_WorkflowId", "material_flows");
        migrationBuilder.Sql("UPDATE `material_flows` SET `WorkflowId` = 0 WHERE `WorkflowId` IS NULL;");
        migrationBuilder.AlterColumn<int>(
            name: "WorkflowId",
            table: "material_flows",
            type: "int",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);
    }
}
