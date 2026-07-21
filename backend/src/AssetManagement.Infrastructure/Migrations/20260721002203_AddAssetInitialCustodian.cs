using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetInitialCustodian : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InitialCustodianId",
                table: "assets",
                type: "int",
                nullable: true);

            // 优先从创建资产时的审计快照还原初始保管人。若历史资产没有创建快照，
            // 仅在从未发生过已通过的保管变更时使用当前保管人，避免伪造历史。
            migrationBuilder.Sql("""
                UPDATE assets AS asset
                LEFT JOIN (
                    SELECT
                        CAST(log.TargetId AS UNSIGNED) AS AssetId,
                        CASE
                            WHEN JSON_TYPE(JSON_EXTRACT(log.Detail, '$.after.CustodianId')) IN ('INTEGER', 'STRING')
                            THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(log.Detail, '$.after.CustodianId')) AS UNSIGNED)
                            ELSE NULL
                        END AS CustodianId
                    FROM audit_logs AS log
                    INNER JOIN (
                        SELECT TargetId, MIN(Id) AS FirstLogId
                        FROM audit_logs
                        WHERE TargetType = 'Asset'
                          AND ActionType = 'POST'
                          AND TargetId REGEXP '^[0-9]+$'
                          AND JSON_VALID(Detail)
                        GROUP BY TargetId
                    ) AS first_log ON first_log.FirstLogId = log.Id
                ) AS initial_record ON initial_record.AssetId = asset.Id
                LEFT JOIN users AS initial_user ON initial_user.Id = initial_record.CustodianId
                SET asset.InitialCustodianId = CASE
                    WHEN initial_user.Id IS NOT NULL THEN initial_user.Id
                    WHEN NOT EXISTS (
                        SELECT 1
                        FROM approval_flows AS flow
                        WHERE flow.AssetId = asset.Id
                          AND flow.Status = 'approved'
                          AND flow.BizType IN ('borrow', 'transfer', 'return')
                    ) THEN asset.CustodianId
                    ELSE NULL
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_assets_InitialCustodianId",
                table: "assets",
                column: "InitialCustodianId");

            migrationBuilder.AddForeignKey(
                name: "FK_assets_users_InitialCustodianId",
                table: "assets",
                column: "InitialCustodianId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_users_InitialCustodianId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_InitialCustodianId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "InitialCustodianId",
                table: "assets");
        }
    }
}
