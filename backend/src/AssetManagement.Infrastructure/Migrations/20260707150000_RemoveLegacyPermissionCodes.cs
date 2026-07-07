using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260707150000_RemoveLegacyPermissionCodes")]
    public partial class RemoveLegacyPermissionCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'material-flow:transfer', '发起料件流转', 'material-flow'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'material-flow:transfer'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'material-flow:approve', '审批料件流转', 'material-flow'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'material-flow:approve'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'user:view', '查看用户', 'user'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'user:view'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'user:assign-role', '分配用户角色', 'user'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'user:assign-role'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'role:view', '查看角色', 'role'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'role:view'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'audit:view', '查看审计日志', 'audit'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'audit:view'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'setting:view', '查看系统参数', 'setting'
                WHERE EXISTS (SELECT 1 FROM `users`)
                  AND NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'setting:view'
                );
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, current_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` current_perm ON current_perm.`Code` = 'material-flow:transfer'
                WHERE legacy_perm.`Code` = 'material:transfer';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, current_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` current_perm ON current_perm.`Code` = 'material-flow:approve'
                WHERE legacy_perm.`Code` = 'material:approve';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, current_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` current_perm ON current_perm.`Code` = 'user:view'
                WHERE legacy_perm.`Code` = 'admin:user';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, current_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` current_perm ON current_perm.`Code` = 'role:view'
                WHERE legacy_perm.`Code` = 'admin:role';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, current_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` current_perm ON current_perm.`Code` = 'audit:view'
                WHERE legacy_perm.`Code` = 'admin:audit';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, current_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` current_perm ON current_perm.`Code` = 'setting:view'
                WHERE legacy_perm.`Code` = 'admin:setting';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT admin_role.`Id`, assign_perm.`Id`
                FROM `roles` admin_role
                INNER JOIN `permissions` assign_perm ON assign_perm.`Code` = 'user:assign-role'
                WHERE admin_role.`Code` = 'admin';
                """);

            migrationBuilder.Sql("""
                DELETE rp
                FROM `role_permissions` rp
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Id` = rp.`PermissionId`
                WHERE legacy_perm.`Code` IN (
                    'material:transfer',
                    'material:approve',
                    'admin:user',
                    'admin:role',
                    'admin:audit',
                    'admin:setting'
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM `permissions`
                WHERE `Code` IN (
                    'material:transfer',
                    'material:approve',
                    'admin:user',
                    'admin:role',
                    'admin:audit',
                    'admin:setting'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp
                FROM `role_permissions` rp
                INNER JOIN `permissions` assign_perm ON assign_perm.`Id` = rp.`PermissionId`
                WHERE assign_perm.`Code` = 'user:assign-role';
                """);

            migrationBuilder.Sql("""
                DELETE FROM `permissions`
                WHERE `Code` = 'user:assign-role';
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'material:transfer', '发起料件流转', 'material'
                WHERE NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'material:transfer'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'material:approve', '审批料件流转', 'material'
                WHERE NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'material:approve'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'admin:user', '用户管理', 'admin'
                WHERE NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'admin:user'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'admin:role', '角色管理', 'admin'
                WHERE NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'admin:role'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'admin:audit', '审计日志', 'admin'
                WHERE NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'admin:audit'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO `permissions` (`Code`, `Name`, `Module`)
                SELECT 'admin:setting', '系统参数', 'admin'
                WHERE NOT EXISTS (
                    SELECT 1 FROM `permissions` WHERE `Code` = 'admin:setting'
                );
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, legacy_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` current_perm ON current_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Code` = 'material:transfer'
                WHERE current_perm.`Code` = 'material-flow:transfer';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, legacy_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` current_perm ON current_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Code` = 'material:approve'
                WHERE current_perm.`Code` = 'material-flow:approve';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, legacy_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` current_perm ON current_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Code` = 'admin:user'
                WHERE current_perm.`Code` = 'user:view';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, legacy_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` current_perm ON current_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Code` = 'admin:role'
                WHERE current_perm.`Code` = 'role:view';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, legacy_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` current_perm ON current_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Code` = 'admin:audit'
                WHERE current_perm.`Code` = 'audit:view';
                """);

            migrationBuilder.Sql("""
                INSERT IGNORE INTO `role_permissions` (`RoleId`, `PermissionId`)
                SELECT rp.`RoleId`, legacy_perm.`Id`
                FROM `role_permissions` rp
                INNER JOIN `permissions` current_perm ON current_perm.`Id` = rp.`PermissionId`
                INNER JOIN `permissions` legacy_perm ON legacy_perm.`Code` = 'admin:setting'
                WHERE current_perm.`Code` = 'setting:view';
                """);
        }
    }
}
