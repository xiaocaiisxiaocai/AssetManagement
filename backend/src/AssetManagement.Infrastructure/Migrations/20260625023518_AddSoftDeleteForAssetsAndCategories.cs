using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteForAssetsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "asset_categories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "asset_categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_assets_IsDeleted",
                table: "assets",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_asset_categories_IsDeleted",
                table: "asset_categories",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assets_IsDeleted",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_asset_categories_IsDeleted",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "asset_categories");
        }
    }
}
