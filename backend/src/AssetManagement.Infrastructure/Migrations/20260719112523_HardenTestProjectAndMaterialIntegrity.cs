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

            migrationBuilder.AddColumn<string>(
                name: "ActiveNameKey",
                table: "test_materials",
                type: "varchar(191)",
                maxLength: 191,
                nullable: true,
                computedColumnSql: "IF(`IsDeleted` = 0, CONCAT(`ProjectId`, ':', `Name`), NULL)",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "business_sequences",
                columns: table => new
                {
                    SequenceKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_sequences", x => x.SequenceKey);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
