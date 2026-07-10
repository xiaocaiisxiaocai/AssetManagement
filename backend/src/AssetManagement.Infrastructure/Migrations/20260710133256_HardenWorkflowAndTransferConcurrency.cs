using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

/// <inheritdoc />
public partial class HardenWorkflowAndTransferConcurrency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<uint>(
            name: "RowVersion",
            table: "test_materials",
            type: "int unsigned",
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.AddColumn<string>(
            name: "ActiveScopeKey",
            table: "material_flows",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<uint>(
            name: "RowVersion",
            table: "assets",
            type: "int unsigned",
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.AddColumn<string>(
            name: "ActiveScopeKey",
            table: "approval_flows",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        // 历史进行中实例必须先纳入唯一锁。若历史库已存在同一资产/料件的重复
        // pending 数据，随后创建唯一索引会明确失败，要求先清理冲突而不是静默放行。
        migrationBuilder.Sql("UPDATE `approval_flows` SET `ActiveScopeKey` = CONCAT('asset:', `AssetId`) WHERE `Status` = 'pending';");
        migrationBuilder.Sql("UPDATE `material_flows` SET `ActiveScopeKey` = CONCAT('material:', `MaterialId`) WHERE `Status` = 'pending';");

        migrationBuilder.CreateIndex(
            name: "IX_material_flows_ActiveScopeKey",
            table: "material_flows",
            column: "ActiveScopeKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_approval_flows_ActiveScopeKey",
            table: "approval_flows",
            column: "ActiveScopeKey",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_material_flows_ActiveScopeKey",
            table: "material_flows");

        migrationBuilder.DropIndex(
            name: "IX_approval_flows_ActiveScopeKey",
            table: "approval_flows");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "test_materials");

        migrationBuilder.DropColumn(
            name: "ActiveScopeKey",
            table: "material_flows");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "assets");

        migrationBuilder.DropColumn(
            name: "ActiveScopeKey",
            table: "approval_flows");
    }
}
