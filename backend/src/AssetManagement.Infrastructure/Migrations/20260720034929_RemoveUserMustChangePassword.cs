using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserMustChangePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "users");

            migrationBuilder.Sql(
                "DELETE FROM system_settings WHERE `Key` = 'security_default_password_backfill_v1';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "INSERT INTO system_settings (`Key`, `Value`, `Description`) " +
                "SELECT 'security_default_password_backfill_v1', 'true', '默认密码账号强制改密治理已完成' " +
                "WHERE NOT EXISTS (SELECT 1 FROM system_settings WHERE `Key` = 'security_default_password_backfill_v1');");
        }
    }
}
