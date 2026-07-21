using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCustodianAfterReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceCustodianId",
                table: "approval_flows",
                type: "int",
                nullable: true);

            // 优先从审批生效审计快照中还原借出前保管人；待审批借用单使用当前保管人，
            // 无审计的旧数据才退回资产登记时的初始保管人。所有候选值均校验用户存在，
            // 避免新增外键时把不完整历史数据误写成有效责任关系。
            migrationBuilder.Sql("""
                UPDATE approval_flows AS flow
                LEFT JOIN assets AS asset ON asset.Id = flow.AssetId
                LEFT JOIN (
                    SELECT
                        CAST(JSON_UNQUOTE(JSON_EXTRACT(
                            CASE WHEN JSON_VALID(log.Detail) THEN log.Detail ELSE NULL END,
                            '$.flowId')) AS UNSIGNED) AS FlowId,
                        MAX(CASE
                            WHEN JSON_TYPE(JSON_EXTRACT(
                                CASE WHEN JSON_VALID(log.Detail) THEN log.Detail ELSE NULL END,
                                '$.before.CustodianId')) IN ('INTEGER', 'STRING')
                            THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(
                                CASE WHEN JSON_VALID(log.Detail) THEN log.Detail ELSE NULL END,
                                '$.before.CustodianId')) AS UNSIGNED)
                            ELSE NULL
                        END) AS SourceCustodianId
                    FROM audit_logs AS log
                    WHERE log.ActionType = 'business'
                      AND log.TargetType = 'Asset'
                      AND JSON_EXTRACT(
                          CASE WHEN JSON_VALID(log.Detail) THEN log.Detail ELSE NULL END,
                          '$.flowId') IS NOT NULL
                    GROUP BY CAST(JSON_UNQUOTE(JSON_EXTRACT(
                        CASE WHEN JSON_VALID(log.Detail) THEN log.Detail ELSE NULL END,
                        '$.flowId')) AS UNSIGNED)
                ) AS audit_source ON audit_source.FlowId = flow.Id
                LEFT JOIN users AS pending_user
                    ON pending_user.Id = CASE WHEN flow.Status = 'pending' THEN asset.CustodianId ELSE NULL END
                LEFT JOIN users AS audit_user ON audit_user.Id = audit_source.SourceCustodianId
                LEFT JOIN users AS initial_user ON initial_user.Id = asset.InitialCustodianId
                SET flow.SourceCustodianId = CASE
                    WHEN flow.Status = 'pending' AND pending_user.Id IS NOT NULL THEN pending_user.Id
                    WHEN audit_user.Id IS NOT NULL THEN audit_user.Id
                    WHEN initial_user.Id IS NOT NULL THEN initial_user.Id
                    ELSE NULL
                END
                WHERE flow.BizType = 'borrow' AND flow.SourceCustodianId IS NULL;
                """);

            // 修复已经归还、当前保管人为空的历史资产：优先恢复最近一次借用的来源保管人，
            // 仅当该用户仍有效且属于资产当前归属部门时生效。
            migrationBuilder.Sql("""
                UPDATE assets AS asset
                INNER JOIN (
                    SELECT flow.AssetId, flow.SourceCustodianId
                    FROM approval_flows AS flow
                    INNER JOIN (
                        SELECT AssetId, MAX(Id) AS FlowId
                        FROM approval_flows
                        WHERE BizType = 'borrow'
                          AND Status = 'approved'
                          AND ConfirmedAt IS NOT NULL
                        GROUP BY AssetId
                    ) AS latest ON latest.FlowId = flow.Id
                ) AS returned_borrow ON returned_borrow.AssetId = asset.Id
                INNER JOIN users AS source_user ON source_user.Id = returned_borrow.SourceCustodianId
                SET asset.CustodianId = source_user.Id,
                    asset.RowVersion = asset.RowVersion + 1
                WHERE asset.Status = 0
                  AND asset.CustodianId IS NULL
                  AND source_user.IsActive = TRUE
                  AND (asset.DepartmentId IS NULL OR source_user.DepartmentId = asset.DepartmentId);
                """);

            // 没有可恢复来源保管人的旧归还数据，由资产直属归属部门的有效负责人接管。
            migrationBuilder.Sql("""
                UPDATE assets AS asset
                INNER JOIN departments AS department ON department.Id = asset.DepartmentId
                INNER JOIN users AS manager_user ON manager_user.Id = department.ManagerId
                SET asset.CustodianId = manager_user.Id,
                    asset.RowVersion = asset.RowVersion + 1
                WHERE asset.Status = 0
                  AND asset.CustodianId IS NULL
                  AND manager_user.IsActive = TRUE
                  AND EXISTS (
                      SELECT 1
                      FROM approval_flows AS flow
                      WHERE flow.AssetId = asset.Id
                        AND flow.BizType = 'borrow'
                        AND flow.Status = 'approved'
                        AND flow.ConfirmedAt IS NOT NULL
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_approval_flows_SourceCustodianId",
                table: "approval_flows",
                column: "SourceCustodianId");

            migrationBuilder.AddForeignKey(
                name: "FK_approval_flows_users_SourceCustodianId",
                table: "approval_flows",
                column: "SourceCustodianId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_approval_flows_users_SourceCustodianId",
                table: "approval_flows");

            migrationBuilder.DropIndex(
                name: "IX_approval_flows_SourceCustodianId",
                table: "approval_flows");

            migrationBuilder.DropColumn(
                name: "SourceCustodianId",
                table: "approval_flows");
        }
    }
}
