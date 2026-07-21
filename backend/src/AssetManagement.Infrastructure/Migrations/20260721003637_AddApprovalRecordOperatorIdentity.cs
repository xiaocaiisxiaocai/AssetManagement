using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalRecordOperatorIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NodeId",
                table: "material_flow_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "OperatorUserId",
                table: "material_flow_records",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeId",
                table: "flow_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "OperatorUserId",
                table: "flow_records",
                type: "int",
                nullable: true);

            // 历史记录只保存了姓名。仅当姓名唯一对应一个用户时才回填稳定身份，
            // 同名人员保持为空，避免把审批记录错误归到其中任意一人名下。
            migrationBuilder.Sql("""
                UPDATE flow_records AS record
                INNER JOIN (
                    SELECT Name, MIN(Id) AS UserId
                    FROM users
                    GROUP BY Name
                    HAVING COUNT(*) = 1
                ) AS unique_user ON unique_user.Name = record.Operator
                SET record.OperatorUserId = unique_user.UserId;

                UPDATE material_flow_records AS record
                INNER JOIN (
                    SELECT Name, MIN(Id) AS UserId
                    FROM users
                    GROUP BY Name
                    HAVING COUNT(*) = 1
                ) AS unique_user ON unique_user.Name = record.Operator
                SET record.OperatorUserId = unique_user.UserId;

                UPDATE flow_records
                SET NodeId = SUBSTRING_INDEX(SUBSTRING(Comment, 4), ':', 1)
                WHERE Action = 'approve' AND Comment LIKE '节点 %:%';

                UPDATE material_flow_records
                SET NodeId = SUBSTRING_INDEX(SUBSTRING(Comment, 4), ':', 1)
                WHERE Action = 'approve' AND Comment LIKE '节点 %:%';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_material_flow_records_OperatorUserId_Action_OperatedAt",
                table: "material_flow_records",
                columns: new[] { "OperatorUserId", "Action", "OperatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_flow_records_OperatorUserId_Action_OperatedAt",
                table: "flow_records",
                columns: new[] { "OperatorUserId", "Action", "OperatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_flow_records_users_OperatorUserId",
                table: "flow_records",
                column: "OperatorUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_material_flow_records_users_OperatorUserId",
                table: "material_flow_records",
                column: "OperatorUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_flow_records_users_OperatorUserId",
                table: "flow_records");

            migrationBuilder.DropForeignKey(
                name: "FK_material_flow_records_users_OperatorUserId",
                table: "material_flow_records");

            migrationBuilder.DropIndex(
                name: "IX_material_flow_records_OperatorUserId_Action_OperatedAt",
                table: "material_flow_records");

            migrationBuilder.DropIndex(
                name: "IX_flow_records_OperatorUserId_Action_OperatedAt",
                table: "flow_records");

            migrationBuilder.DropColumn(
                name: "NodeId",
                table: "material_flow_records");

            migrationBuilder.DropColumn(
                name: "OperatorUserId",
                table: "material_flow_records");

            migrationBuilder.DropColumn(
                name: "NodeId",
                table: "flow_records");

            migrationBuilder.DropColumn(
                name: "OperatorUserId",
                table: "flow_records");
        }
    }
}
