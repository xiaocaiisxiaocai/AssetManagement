using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLocationDictionaryWithText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "test_materials",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "assets",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE `assets` a
                INNER JOIN `locations` l ON l.`Id` = a.`LocationId`
                SET a.`LocationName` = l.`Name`
                WHERE a.`LocationId` IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE `test_materials` m
                INNER JOIN `locations` l ON l.`Id` = m.`LocationId`
                SET m.`LocationName` = l.`Name`
                WHERE m.`LocationId` IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_assets_locations_LocationId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_test_materials_locations_LocationId",
                table: "test_materials");

            migrationBuilder.DropIndex(
                name: "IX_test_materials_LocationId",
                table: "test_materials");

            migrationBuilder.DropIndex(
                name: "IX_assets_LocationId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "test_materials");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "assets");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.Sql("""
                DELETE rm
                FROM `role_menus` rm
                INNER JOIN `menus` m ON m.`Id` = rm.`MenuId`
                WHERE m.`Name` = 'AssetLocations' OR m.`Path` = '/asset/locations';
                """);
            migrationBuilder.Sql("DELETE FROM `menus` WHERE `Name` = 'AssetLocations' OR `Path` = '/asset/locations';");
            migrationBuilder.Sql("""
                DELETE rp
                FROM `role_permissions` rp
                INNER JOIN `permissions` p ON p.`Id` = rp.`PermissionId`
                WHERE p.`Code` LIKE 'location:%';
                """);
            migrationBuilder.Sql("DELETE FROM `permissions` WHERE `Code` LIKE 'location:%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "test_materials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "assets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_LocationId",
                table: "test_materials",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_LocationId",
                table: "assets",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_locations_Name",
                table: "locations",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `locations` (`Name`)
                SELECT `LocationName` FROM `assets`
                WHERE `LocationName` IS NOT NULL AND TRIM(`LocationName`) <> ''
                UNION
                SELECT `LocationName` FROM `test_materials`
                WHERE `LocationName` IS NOT NULL AND TRIM(`LocationName`) <> '';
                """);

            migrationBuilder.Sql("""
                UPDATE `assets` a
                INNER JOIN `locations` l ON l.`Name` = a.`LocationName`
                SET a.`LocationId` = l.`Id`
                WHERE a.`LocationName` IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE `test_materials` m
                INNER JOIN `locations` l ON l.`Name` = m.`LocationName`
                SET m.`LocationId` = l.`Id`
                WHERE m.`LocationName` IS NOT NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_locations_LocationId",
                table: "assets",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_materials_locations_LocationId",
                table: "test_materials",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "test_materials");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "assets");

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'location:view', '查看存放位置', 'location'
                WHERE NOT EXISTS (SELECT 1 FROM `permissions` WHERE `Code` = 'location:view');
                """);
            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'location:create', '新增存放位置', 'location'
                WHERE NOT EXISTS (SELECT 1 FROM `permissions` WHERE `Code` = 'location:create');
                """);
            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'location:edit', '编辑存放位置', 'location'
                WHERE NOT EXISTS (SELECT 1 FROM `permissions` WHERE `Code` = 'location:edit');
                """);
            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'location:delete', '删除存放位置', 'location'
                WHERE NOT EXISTS (SELECT 1 FROM `permissions` WHERE `Code` = 'location:delete');
                """);

            migrationBuilder.Sql("""
                INSERT INTO `menus` (`ParentId`, `Name`, `Title`, `Path`, `Component`, `Icon`, `Sort`, `Type`, `PermissionCode`)
                SELECT parent.`Id`, 'AssetLocations', '存放位置', '/asset/locations', '/asset/locations/index', NULL, 14, 'menu', 'location:view'
                FROM `menus` parent
                WHERE parent.`Name` = 'Asset'
                  AND NOT EXISTS (SELECT 1 FROM `menus` WHERE `Name` = 'AssetLocations');
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT r.`Id`, p.`Id`
                FROM `roles` r
                INNER JOIN `permissions` p ON p.`Code` LIKE 'location:%'
                WHERE r.`Code` = 'admin'
                   OR (r.`Code` IN ('supervisor', 'employee') AND p.`Code` = 'location:view');
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_menus` (`RoleId`, `MenuId`)
                SELECT r.`Id`, m.`Id`
                FROM `roles` r
                INNER JOIN `menus` m ON m.`Name` = 'AssetLocations'
                WHERE r.`Code` IN ('admin', 'supervisor');
                """);
        }
    }
}
