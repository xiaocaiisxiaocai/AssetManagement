using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateMaterialPagesIntoProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE menus
                SET Title = '新产品新技术跟进'
                WHERE Name = 'Material';
                """);

            migrationBuilder.Sql("""
                DELETE FROM role_menus
                WHERE MenuId IN (
                    SELECT Id FROM menus WHERE Name IN ('MaterialList', 'MaterialTransfers')
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM menus
                WHERE Name IN ('MaterialList', 'MaterialTransfers');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE menus
                SET Title = '测试料件'
                WHERE Name = 'Material';
                """);

            migrationBuilder.Sql("""
                INSERT INTO menus (Id, ParentId, Name, Title, Path, Component, Icon, Sort, Type, PermissionCode)
                SELECT COALESCE(MAX(Id), 0) + 1, (SELECT Id FROM menus WHERE Name = 'Material'),
                       'MaterialList', '料件清单', '/material/list', '/material/list/index',
                       NULL, 16, 'menu', 'material:view'
                FROM menus
                WHERE NOT EXISTS (SELECT 1 FROM menus WHERE Name = 'MaterialList');
                """);

            migrationBuilder.Sql("""
                INSERT INTO menus (Id, ParentId, Name, Title, Path, Component, Icon, Sort, Type, PermissionCode)
                SELECT COALESCE(MAX(Id), 0) + 1, (SELECT Id FROM menus WHERE Name = 'Material'),
                       'MaterialTransfers', '流转审批', '/material/transfers', '/material/transfers/index',
                       NULL, 18, 'menu', 'material:transfer'
                FROM menus
                WHERE NOT EXISTS (SELECT 1 FROM menus WHERE Name = 'MaterialTransfers');
                """);
        }
    }
}
