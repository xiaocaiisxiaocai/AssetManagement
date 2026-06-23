using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToBpmnWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nodes",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "CurrentNodeIndex",
                table: "approval_flows");

            migrationBuilder.RenameColumn(
                name: "Nodes",
                table: "approval_flows",
                newName: "CurrentNodeIds");

            migrationBuilder.AddColumn<string>(
                name: "BpmnXml",
                table: "workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BpmnTokens",
                table: "approval_flows",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BpmnXml",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "BpmnTokens",
                table: "approval_flows");

            migrationBuilder.RenameColumn(
                name: "CurrentNodeIds",
                table: "approval_flows",
                newName: "Nodes");

            migrationBuilder.AddColumn<string>(
                name: "Nodes",
                table: "workflows",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CurrentNodeIndex",
                table: "approval_flows",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
