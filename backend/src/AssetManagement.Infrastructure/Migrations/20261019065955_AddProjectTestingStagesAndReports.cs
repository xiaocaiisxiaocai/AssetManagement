using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTestingStagesAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_test_stages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Objective = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TestContent = table.Column<string>(type: "text", maxLength: 8000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TestResult = table.Column<string>(type: "text", maxLength: 8000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Conclusion = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartDate = table.Column<DateTime>(type: "date", nullable: true),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RowVersion = table.Column<uint>(type: "int unsigned", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActiveSequence = table.Column<int>(type: "int", nullable: true, computedColumnSql: "IF(`IsDeleted` = 0, `Sequence`, NULL)", stored: true),
                    CurrentProjectKey = table.Column<int>(type: "int", nullable: true, computedColumnSql: "IF(`IsDeleted` = 0 AND `Status` IN ('in_progress', 'blocked'), `ProjectId`, NULL)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_test_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_test_stages_test_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "test_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "project_report_documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoredName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedById = table.Column<int>(type: "int", nullable: false),
                    UploadedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_report_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_report_documents_project_test_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "project_test_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_report_documents_test_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "test_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_report_documents_users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_project_report_documents_ProjectId_IsDeleted",
                table: "project_report_documents",
                columns: new[] { "ProjectId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_project_report_documents_StageId",
                table: "project_report_documents",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_project_report_documents_StoredName",
                table: "project_report_documents",
                column: "StoredName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_report_documents_UploadedById",
                table: "project_report_documents",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_project_test_stages_CurrentProjectKey",
                table: "project_test_stages",
                column: "CurrentProjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_test_stages_ProjectId_ActiveSequence",
                table: "project_test_stages",
                columns: new[] { "ProjectId", "ActiveSequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_report_documents");

            migrationBuilder.DropTable(
                name: "project_test_stages");
        }
    }
}
