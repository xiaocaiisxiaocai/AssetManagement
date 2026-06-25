using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTestMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_flow_records",
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
                    table.PrimaryKey("PK_material_flow_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "material_flows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BizType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    WorkflowId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplicantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Applicant = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TransfereeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Transferee = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TransfereeDept = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CurrentNodeIds = table.Column<string>(type: "TEXT", nullable: false),
                    BpmnTokens = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Deadline = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_flows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "test_materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaterialNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    VendorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustodianId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageUrls = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "test_projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_projects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_material_flow_records_FlowId",
                table: "material_flow_records",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_material_flows_ApplicantId",
                table: "material_flows",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_material_flows_FlowNo",
                table: "material_flows",
                column: "FlowNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_flows_MaterialId",
                table: "material_flows",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_material_flows_Status",
                table: "material_flows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_DepartmentId",
                table: "test_materials",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_IsDeleted",
                table: "test_materials",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_MaterialNo",
                table: "test_materials",
                column: "MaterialNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_ProjectId",
                table: "test_materials",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_Status",
                table: "test_materials",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_test_projects_IsDeleted",
                table: "test_projects",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material_flow_records");

            migrationBuilder.DropTable(
                name: "material_flows");

            migrationBuilder.DropTable(
                name: "test_materials");

            migrationBuilder.DropTable(
                name: "test_projects");
        }
    }
}
