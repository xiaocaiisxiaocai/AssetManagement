using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationLevelId",
                table: "departments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organization_levels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_levels", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_departments_OrganizationLevelId",
                table: "departments",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_levels_Code",
                table: "organization_levels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_levels_Sort",
                table: "organization_levels",
                column: "Sort");

            migrationBuilder.InsertData(
                table: "organization_levels",
                columns: new[] { "Id", "Code", "Name", "Sort", "IsActive" },
                values: new object[,]
                {
                    { 1, "company", "公司/中心", 10, true },
                    { 2, "division", "事业部", 20, true },
                    { 3, "department", "部门", 30, true },
                    { 4, "section", "课别", 40, true }
                });

            migrationBuilder.Sql("""
                UPDATE departments
                SET OrganizationLevelId = 1
                WHERE ParentId IS NULL;

                UPDATE departments
                SET OrganizationLevelId = 2
                WHERE Name LIKE '%事业部%';

                UPDATE departments AS child
                INNER JOIN departments AS parent ON parent.Id = child.ParentId
                SET child.OrganizationLevelId = 3
                WHERE child.OrganizationLevelId IS NULL
                  AND parent.OrganizationLevelId IN (1, 2);

                UPDATE departments
                SET OrganizationLevelId = 4
                WHERE OrganizationLevelId IS NULL;

                UPDATE workflows
                SET BpmnXml = REPLACE(REPLACE(REPLACE(REPLACE(
                    BpmnXml,
                    'camunda:assignee="sectionManager"', 'camunda:assignee="orgManager:section"'),
                    'camunda:assignee="departmentManager"', 'camunda:assignee="orgManager:department"'),
                    '${requiresSectionApproval}', '${requiresApproval_section}'),
                    '${requiresDepartmentApproval}', '${requiresApproval_department}')
                WHERE IsActive = 1;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_departments_organization_levels_OrganizationLevelId",
                table: "departments",
                column: "OrganizationLevelId",
                principalTable: "organization_levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_departments_organization_levels_OrganizationLevelId",
                table: "departments");

            migrationBuilder.DropTable(
                name: "organization_levels");

            migrationBuilder.DropIndex(
                name: "IX_departments_OrganizationLevelId",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "OrganizationLevelId",
                table: "departments");
        }
    }
}
