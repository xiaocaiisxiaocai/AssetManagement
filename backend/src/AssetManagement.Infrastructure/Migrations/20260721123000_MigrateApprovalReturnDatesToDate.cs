using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721123000_MigrateApprovalReturnDatesToDate")]
public partial class MigrateApprovalReturnDatesToDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE approval_flows SET ReturnDate = TRIM(ReturnDate) WHERE ReturnDate IS NOT NULL;");
        migrationBuilder.Sql("UPDATE approval_flows SET OriginalReturnDate = TRIM(OriginalReturnDate) WHERE OriginalReturnDate IS NOT NULL;");
        // 旧版 MySQL 曾将日期值回写为 yyyy-MM-dd 00:00:00，迁移前先无损规范化。
        migrationBuilder.Sql("""
            UPDATE approval_flows
            SET ReturnDate = DATE_FORMAT(STR_TO_DATE(LEFT(ReturnDate, 19), '%Y-%m-%d %H:%i:%s'), '%Y-%m-%d')
            WHERE ReturnDate REGEXP '^(1[0-9]{3}|[2-9][0-9]{3})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01]) [0-2][0-9]:[0-5][0-9]:[0-5][0-9](\\.[0-9]+)?$'
              AND STR_TO_DATE(LEFT(ReturnDate, 19), '%Y-%m-%d %H:%i:%s') IS NOT NULL;
            """);
        migrationBuilder.Sql("""
            UPDATE approval_flows
            SET OriginalReturnDate = DATE_FORMAT(STR_TO_DATE(LEFT(OriginalReturnDate, 19), '%Y-%m-%d %H:%i:%s'), '%Y-%m-%d')
            WHERE OriginalReturnDate REGEXP '^(1[0-9]{3}|[2-9][0-9]{3})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01]) [0-2][0-9]:[0-5][0-9]:[0-5][0-9](\\.[0-9]+)?$'
              AND STR_TO_DATE(LEFT(OriginalReturnDate, 19), '%Y-%m-%d %H:%i:%s') IS NOT NULL;
            """);
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS validate_approval_return_dates_20260721;");
        migrationBuilder.Sql("""
            CREATE PROCEDURE validate_approval_return_dates_20260721()
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM approval_flows
                    WHERE (ReturnDate IS NOT NULL AND
                           (ReturnDate NOT REGEXP '^(1[0-9]{3}|[2-9][0-9]{3})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$'
                            OR STR_TO_DATE(ReturnDate, '%Y-%m-%d') IS NULL
                            OR CAST(SUBSTRING(ReturnDate, 9, 2) AS UNSIGNED) >
                               DAY(LAST_DAY(CONCAT(SUBSTRING(ReturnDate, 1, 7), '-01')))))
                       OR (OriginalReturnDate IS NOT NULL AND
                           (OriginalReturnDate NOT REGEXP '^(1[0-9]{3}|[2-9][0-9]{3})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$'
                            OR STR_TO_DATE(OriginalReturnDate, '%Y-%m-%d') IS NULL
                            OR CAST(SUBSTRING(OriginalReturnDate, 9, 2) AS UNSIGNED) >
                               DAY(LAST_DAY(CONCAT(SUBSTRING(OriginalReturnDate, 1, 7), '-01')))))
                    LIMIT 1
                ) THEN
                    SIGNAL SQLSTATE '45000'
                        SET MESSAGE_TEXT = '归还日期存在非法历史值，请修复 approval_flows 后重试迁移';
                END IF;
            END;
            """);
        migrationBuilder.Sql("CALL validate_approval_return_dates_20260721();");
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS validate_approval_return_dates_20260721;");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "ReturnDate",
            table: "approval_flows",
            type: "date",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "OriginalReturnDate",
            table: "approval_flows",
            type: "date",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_approval_flows_BizType_Status_ConfirmedAt_ReturnDate",
            table: "approval_flows",
            columns: new[] { "BizType", "Status", "ConfirmedAt", "ReturnDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_approval_flows_BizType_Status_ConfirmedAt_ReturnDate",
            table: "approval_flows");

        migrationBuilder.AlterColumn<string>(
            name: "ReturnDate",
            table: "approval_flows",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "OriginalReturnDate",
            table: "approval_flows",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("""
            UPDATE approval_flows
            SET ReturnDate = DATE_FORMAT(STR_TO_DATE(ReturnDate, '%Y-%m-%d'), '%Y-%m-%d')
            WHERE ReturnDate IS NOT NULL;
            """);
        migrationBuilder.Sql("""
            UPDATE approval_flows
            SET OriginalReturnDate = DATE_FORMAT(STR_TO_DATE(OriginalReturnDate, '%Y-%m-%d'), '%Y-%m-%d')
            WHERE OriginalReturnDate IS NOT NULL;
            """);
    }
}
