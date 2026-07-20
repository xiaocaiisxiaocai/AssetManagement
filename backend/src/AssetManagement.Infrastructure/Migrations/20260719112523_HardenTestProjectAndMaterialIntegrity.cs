using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenTestProjectAndMaterialIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL 5.7 DDL 会隐式提交。必须在第一条 DDL 前一次性检查历史脏数据，
            // 避免后续索引/FK 失败时留下“部分升级但无迁移记录”的不可重试状态。
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `assert_harden_test_project_integrity`");
            migrationBuilder.Sql("""
                CREATE PROCEDURE `assert_harden_test_project_integrity`()
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM `test_projects`
                        WHERE `Code` IS NOT NULL
                        GROUP BY `Code` HAVING COUNT(*) > 1
                    ) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate project Code';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM `test_projects`
                        GROUP BY `Name` HAVING COUNT(*) > 1
                    ) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate project Name';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM `test_materials`
                        WHERE `IsDeleted` = 0
                        GROUP BY `ProjectId`, `Name` HAVING COUNT(*) > 1
                    ) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate active material Name';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM `test_materials` m
                        LEFT JOIN `test_projects` p ON p.`Id` = m.`ProjectId`
                        WHERE p.`Id` IS NULL
                    ) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan test material';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM `test_project_followups` f
                        LEFT JOIN `test_projects` p ON p.`Id` = f.`ProjectId`
                        WHERE p.`Id` IS NULL
                    ) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan project followup';
                    END IF;
                END
                """);
            migrationBuilder.Sql("CALL `assert_harden_test_project_integrity`()");
            migrationBuilder.Sql("DROP PROCEDURE `assert_harden_test_project_integrity`");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `reset_partial_test_project_integrity`");
            migrationBuilder.Sql("""
                CREATE PROCEDURE `reset_partial_test_project_integrity`()
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND CONSTRAINT_NAME='FK_test_materials_test_projects_ProjectId') THEN
                        ALTER TABLE `test_materials` DROP FOREIGN KEY `FK_test_materials_test_projects_ProjectId`;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_project_followups' AND CONSTRAINT_NAME='FK_test_project_followups_test_projects_ProjectId') THEN
                        ALTER TABLE `test_project_followups` DROP FOREIGN KEY `FK_test_project_followups_test_projects_ProjectId`;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_projects' AND INDEX_NAME='IX_test_projects_Code') THEN
                        DROP INDEX `IX_test_projects_Code` ON `test_projects`;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_projects' AND INDEX_NAME='IX_test_projects_Name') THEN
                        DROP INDEX `IX_test_projects_Name` ON `test_projects`;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND INDEX_NAME='IX_test_materials_ActiveNameKey') THEN
                        DROP INDEX `IX_test_materials_ActiveNameKey` ON `test_materials`;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND COLUMN_NAME='ActiveNameKey') THEN
                        ALTER TABLE `test_materials` DROP COLUMN `ActiveNameKey`;
                    END IF;
                END
                """);
            migrationBuilder.Sql("CALL `reset_partial_test_project_integrity`()");
            migrationBuilder.Sql("DROP PROCEDURE `reset_partial_test_project_integrity`");

            migrationBuilder.AddColumn<string>(
                name: "ActiveNameKey",
                table: "test_materials",
                type: "varchar(191)",
                maxLength: 191,
                nullable: true,
                computedColumnSql: "IF(`IsDeleted` = 0, CONCAT(`ProjectId`, ':', `Name`), NULL)",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // CREATE TABLE 是单条原子 DDL，不存在“半张表”需要删除重建。
            // 若上次已执行到最后、但迁移历史还未写入就断开，重入时必须保留
            // 已分配的序列值，否则可能重用已预留的业务编号。
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `business_sequences` (
                    `SequenceKey` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
                    `NextValue` int NOT NULL,
                    CONSTRAINT `PK_business_sequences` PRIMARY KEY (`SequenceKey`)
                ) CHARACTER SET=utf8mb4
                """);

            migrationBuilder.CreateIndex(
                name: "IX_test_projects_Code",
                table: "test_projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_projects_Name",
                table: "test_projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_ActiveNameKey",
                table: "test_materials",
                column: "ActiveNameKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_test_materials_test_projects_ProjectId",
                table: "test_materials",
                column: "ProjectId",
                principalTable: "test_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_project_followups_test_projects_ProjectId",
                table: "test_project_followups",
                column: "ProjectId",
                principalTable: "test_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_test_materials_test_projects_ProjectId",
                table: "test_materials");

            migrationBuilder.DropForeignKey(
                name: "FK_test_project_followups_test_projects_ProjectId",
                table: "test_project_followups");

            migrationBuilder.DropTable(
                name: "business_sequences");

            migrationBuilder.DropIndex(
                name: "IX_test_projects_Code",
                table: "test_projects");

            migrationBuilder.DropIndex(
                name: "IX_test_projects_Name",
                table: "test_projects");

            migrationBuilder.DropIndex(
                name: "IX_test_materials_ActiveNameKey",
                table: "test_materials");

            migrationBuilder.DropColumn(
                name: "ActiveNameKey",
                table: "test_materials");
        }
    }
}
