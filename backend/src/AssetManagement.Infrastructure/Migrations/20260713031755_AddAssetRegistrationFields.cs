using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentCondition",
                table: "assets",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstRegistration",
                table: "assets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationTime",
                table: "assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "assets",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentCondition",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "IsFirstRegistration",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "RegistrationTime",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "assets");
        }
    }
}
