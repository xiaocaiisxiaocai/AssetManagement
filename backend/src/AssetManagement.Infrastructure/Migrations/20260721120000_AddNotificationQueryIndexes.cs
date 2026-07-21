using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721120000_AddNotificationQueryIndexes")]
public partial class AddNotificationQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_notifications_UserId_CreatedAt_Id",
            table: "notifications",
            columns: new[] { "UserId", "CreatedAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_UserId_IsRead_CreatedAt_Id",
            table: "notifications",
            columns: new[] { "UserId", "IsRead", "CreatedAt", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_notifications_UserId_CreatedAt_Id",
            table: "notifications");

        migrationBuilder.DropIndex(
            name: "IX_notifications_UserId_IsRead_CreatedAt_Id",
            table: "notifications");
    }
}
