using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260722114000_GrantSupervisorDepartmentMaintenance")]
public partial class GrantSupervisorDepartmentMaintenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT IGNORE INTO role_permissions (RoleId, PermissionId)
            SELECT roles.Id, permissions.Id
            FROM roles
            JOIN permissions ON permissions.Code IN (
                'department:create', 'department:edit', 'department:delete'
            )
            WHERE roles.Code = 'supervisor';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE role_permissions
            FROM role_permissions
            JOIN roles ON roles.Id = role_permissions.RoleId
            JOIN permissions ON permissions.Id = role_permissions.PermissionId
            WHERE roles.Code = 'supervisor'
              AND permissions.Code IN (
                  'department:create', 'department:edit', 'department:delete'
              );
            """);
    }
}
