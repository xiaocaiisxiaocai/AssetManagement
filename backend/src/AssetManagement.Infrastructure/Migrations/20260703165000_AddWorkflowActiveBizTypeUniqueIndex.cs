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
            migrationBuilder.Sql(
                "ALTER TABLE `workflows` ADD COLUMN `ActiveBizType` varchar(50) GENERATED ALWAYS AS (CASE WHEN `IsActive` THEN `BizType` ELSE NULL END) STORED;");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_ActiveBizType",
                table: "workflows",
                column: "ActiveBizType",
                unique: true);
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
