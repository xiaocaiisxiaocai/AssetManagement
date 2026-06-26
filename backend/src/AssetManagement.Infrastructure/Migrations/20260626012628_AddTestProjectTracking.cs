using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTestProjectTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "test_projects");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedDate",
                table: "test_projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpIntervalDays",
                table: "test_projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "test_projects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedFinishDate",
                table: "test_projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressCode",
                table: "test_projects",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectTypeCode",
                table: "test_projects",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "test_projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestStatus",
                table: "test_projects",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "test_project_followups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    FilledById = table.Column<int>(type: "INTEGER", nullable: false),
                    FilledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_project_followups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "test_project_options",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Sort = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_project_options", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_test_projects_OwnerId",
                table: "test_projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_test_project_followups_DueDate",
                table: "test_project_followups",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_test_project_followups_ProjectId",
                table: "test_project_followups",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_test_project_options_Kind_Code",
                table: "test_project_options",
                columns: new[] { "Kind", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "test_project_followups");

            migrationBuilder.DropTable(
                name: "test_project_options");

            migrationBuilder.DropIndex(
                name: "IX_test_projects_OwnerId",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "ClosedDate",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "FollowUpIntervalDays",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "PlannedFinishDate",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "ProgressCode",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "ProjectTypeCode",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "test_projects");

            migrationBuilder.DropColumn(
                name: "TestStatus",
                table: "test_projects");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "test_projects",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }
    }
}
