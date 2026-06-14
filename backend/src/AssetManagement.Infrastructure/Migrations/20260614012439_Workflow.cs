using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Workflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_flows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BizType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    WorkflowId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplicantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Applicant = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TransfereeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Transferee = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TransfereeDept = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReturnDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CurrentNodeIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Nodes = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Deadline = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_flows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "flow_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Operator = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OperatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flow_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BizType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Nodes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_flows_ApplicantId",
                table: "approval_flows",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_flows_AssetId",
                table: "approval_flows",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_flows_FlowNo",
                table: "approval_flows",
                column: "FlowNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_flows_Status",
                table: "approval_flows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_flow_records_FlowId",
                table: "flow_records",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_records_OperatedAt",
                table: "flow_records",
                column: "OperatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_BizType",
                table: "workflows",
                column: "BizType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_flows");

            migrationBuilder.DropTable(
                name: "flow_records");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
