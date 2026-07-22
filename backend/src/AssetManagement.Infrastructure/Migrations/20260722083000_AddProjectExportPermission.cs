using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260722083000_AddProjectExportPermission")]
public partial class AddProjectExportPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO permissions (Code, Name, Module)
            SELECT 'project:export', '导出测试项目', 'project'
            WHERE EXISTS (SELECT 1 FROM users LIMIT 1)
              AND NOT EXISTS (SELECT 1 FROM permissions WHERE Code = 'project:export');
            """);
        migrationBuilder.Sql("""
            INSERT IGNORE INTO role_permissions (RoleId, PermissionId)
            SELECT roles.Id, permissions.Id
            FROM roles
            JOIN permissions ON permissions.Code IN ('asset:export', 'project:export')
            WHERE roles.Code IN ('admin', 'supervisor');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE role_permissions
            FROM role_permissions
            JOIN permissions ON permissions.Id = role_permissions.PermissionId
            WHERE permissions.Code = 'project:export';
            """);
        migrationBuilder.Sql("DELETE FROM permissions WHERE Code = 'project:export';");
    }
}
