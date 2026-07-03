using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowDisabledDuplicateWorkflowBizType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workflows_BizType",
                table: "workflows");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_BizType",
                table: "workflows",
                column: "BizType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workflows_BizType",
                table: "workflows");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_BizType",
                table: "workflows",
                column: "BizType",
                unique: true);
        }
    }
}
