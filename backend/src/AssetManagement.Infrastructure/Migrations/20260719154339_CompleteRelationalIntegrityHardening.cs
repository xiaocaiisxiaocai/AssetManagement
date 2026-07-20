using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteRelationalIntegrityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL 5.7 的 DDL 会隐式提交。所有会导致唯一索引或外键创建失败的
            // 历史数据必须在第一条业务表 DDL 之前一次性发现，避免半升级状态。
            // 审计日志允许匿名/已删除用户；保留日志，仅清空旧版遗留的失效可空引用。
            migrationBuilder.Sql("""
                UPDATE `audit_logs` c
                LEFT JOIN `users` p ON p.`Id` = c.`UserId`
                SET c.`UserId` = NULL
                WHERE c.`UserId` IS NOT NULL AND p.`Id` IS NULL
                """);
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `assert_complete_relational_integrity`");
            migrationBuilder.Sql("""
                CREATE PROCEDURE `assert_complete_relational_integrity`()
                BEGIN
                    IF EXISTS (SELECT 1 FROM `roles` GROUP BY `Name` HAVING COUNT(*) > 1) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate role name';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `locations` GROUP BY `Name` HAVING COUNT(*) > 1) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate location name';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `departments` GROUP BY `Name` HAVING COUNT(*) > 1) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: duplicate department name';
                    END IF;

                    IF EXISTS (SELECT 1 FROM `approval_flows` c LEFT JOIN `assets` p ON p.`Id` = c.`AssetId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan approval asset';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `approval_flows` c LEFT JOIN `users` p ON p.`Id` = c.`ApplicantId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan approval applicant';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `approval_flows` c LEFT JOIN `users` p ON p.`Id` = c.`TransfereeId` WHERE c.`TransfereeId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan approval transferee';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `approval_flows` c LEFT JOIN `workflows` p ON p.`Id` = c.`WorkflowId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan approval workflow';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `asset_categories` c LEFT JOIN `asset_categories` p ON p.`Id` = c.`ParentId` WHERE c.`ParentId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan category parent';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `assets` c LEFT JOIN `asset_categories` p ON p.`Id` = c.`CategoryId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan asset category';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `assets` c LEFT JOIN `departments` p ON p.`Id` = c.`DepartmentId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan asset department';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `assets` c LEFT JOIN `locations` p ON p.`Id` = c.`LocationId` WHERE c.`LocationId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan asset location';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `assets` c LEFT JOIN `users` p ON p.`Id` = c.`CustodianId` WHERE c.`CustodianId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan asset custodian';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `departments` c LEFT JOIN `departments` p ON p.`Id` = c.`ParentId` WHERE c.`ParentId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan department parent';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `departments` c LEFT JOIN `users` p ON p.`Id` = c.`ManagerId` WHERE c.`ManagerId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan department manager';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `flow_records` c LEFT JOIN `approval_flows` p ON p.`Id` = c.`FlowId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan approval record';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `material_flow_records` c LEFT JOIN `material_flows` p ON p.`Id` = c.`FlowId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material record';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `menus` c LEFT JOIN `menus` p ON p.`Id` = c.`ParentId` WHERE c.`ParentId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan menu parent';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `notifications` c LEFT JOIN `users` p ON p.`Id` = c.`UserId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan notification user';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `test_materials` c LEFT JOIN `departments` p ON p.`Id` = c.`DepartmentId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material department';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `test_materials` c LEFT JOIN `locations` p ON p.`Id` = c.`LocationId` WHERE c.`LocationId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material location';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `test_materials` c LEFT JOIN `users` p ON p.`Id` = c.`CustodianId` WHERE c.`CustodianId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan material custodian';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `test_project_followups` c LEFT JOIN `users` p ON p.`Id` = c.`FilledById` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan followup user';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `test_projects` c LEFT JOIN `users` p ON p.`Id` = c.`OwnerId` WHERE p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan project owner';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `users` c LEFT JOIN `departments` p ON p.`Id` = c.`DepartmentId` WHERE c.`DepartmentId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan user department';
                    END IF;
                    IF EXISTS (SELECT 1 FROM `users` c LEFT JOIN `users` p ON p.`Id` = c.`SupervisorId` WHERE c.`SupervisorId` IS NOT NULL AND p.`Id` IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'migration blocked: orphan user supervisor';
                    END IF;
                END
                """);
            migrationBuilder.Sql("CALL `assert_complete_relational_integrity`()");
            migrationBuilder.Sql("DROP PROCEDURE `assert_complete_relational_integrity`");

            // MySQL DDL 会隐式提交：若上次执行在中途断开，迁移历史不会落库，
            // 但前半段对象已经存在。先移除本迁移负责的同名对象再完整重建，
            // 使下次启动可以从任意断点安全收敛。
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `reset_partial_relational_hardening`");
            migrationBuilder.Sql("""
                CREATE PROCEDURE `reset_partial_relational_hardening`()
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='approval_flows' AND CONSTRAINT_NAME='FK_approval_flows_assets_AssetId') THEN ALTER TABLE `approval_flows` DROP FOREIGN KEY `FK_approval_flows_assets_AssetId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='approval_flows' AND CONSTRAINT_NAME='FK_approval_flows_users_ApplicantId') THEN ALTER TABLE `approval_flows` DROP FOREIGN KEY `FK_approval_flows_users_ApplicantId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='approval_flows' AND CONSTRAINT_NAME='FK_approval_flows_users_TransfereeId') THEN ALTER TABLE `approval_flows` DROP FOREIGN KEY `FK_approval_flows_users_TransfereeId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='approval_flows' AND CONSTRAINT_NAME='FK_approval_flows_workflows_WorkflowId') THEN ALTER TABLE `approval_flows` DROP FOREIGN KEY `FK_approval_flows_workflows_WorkflowId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='asset_categories' AND CONSTRAINT_NAME='FK_asset_categories_asset_categories_ParentId') THEN ALTER TABLE `asset_categories` DROP FOREIGN KEY `FK_asset_categories_asset_categories_ParentId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='assets' AND CONSTRAINT_NAME='FK_assets_asset_categories_CategoryId') THEN ALTER TABLE `assets` DROP FOREIGN KEY `FK_assets_asset_categories_CategoryId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='assets' AND CONSTRAINT_NAME='FK_assets_departments_DepartmentId') THEN ALTER TABLE `assets` DROP FOREIGN KEY `FK_assets_departments_DepartmentId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='assets' AND CONSTRAINT_NAME='FK_assets_locations_LocationId') THEN ALTER TABLE `assets` DROP FOREIGN KEY `FK_assets_locations_LocationId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='assets' AND CONSTRAINT_NAME='FK_assets_users_CustodianId') THEN ALTER TABLE `assets` DROP FOREIGN KEY `FK_assets_users_CustodianId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='audit_logs' AND CONSTRAINT_NAME='FK_audit_logs_users_UserId') THEN ALTER TABLE `audit_logs` DROP FOREIGN KEY `FK_audit_logs_users_UserId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='departments' AND CONSTRAINT_NAME='FK_departments_departments_ParentId') THEN ALTER TABLE `departments` DROP FOREIGN KEY `FK_departments_departments_ParentId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='departments' AND CONSTRAINT_NAME='FK_departments_users_ManagerId') THEN ALTER TABLE `departments` DROP FOREIGN KEY `FK_departments_users_ManagerId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='flow_records' AND CONSTRAINT_NAME='FK_flow_records_approval_flows_FlowId') THEN ALTER TABLE `flow_records` DROP FOREIGN KEY `FK_flow_records_approval_flows_FlowId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='material_flow_records' AND CONSTRAINT_NAME='FK_material_flow_records_material_flows_FlowId') THEN ALTER TABLE `material_flow_records` DROP FOREIGN KEY `FK_material_flow_records_material_flows_FlowId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='menus' AND CONSTRAINT_NAME='FK_menus_menus_ParentId') THEN ALTER TABLE `menus` DROP FOREIGN KEY `FK_menus_menus_ParentId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='notifications' AND CONSTRAINT_NAME='FK_notifications_users_UserId') THEN ALTER TABLE `notifications` DROP FOREIGN KEY `FK_notifications_users_UserId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND CONSTRAINT_NAME='FK_test_materials_departments_DepartmentId') THEN ALTER TABLE `test_materials` DROP FOREIGN KEY `FK_test_materials_departments_DepartmentId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND CONSTRAINT_NAME='FK_test_materials_locations_LocationId') THEN ALTER TABLE `test_materials` DROP FOREIGN KEY `FK_test_materials_locations_LocationId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND CONSTRAINT_NAME='FK_test_materials_users_CustodianId') THEN ALTER TABLE `test_materials` DROP FOREIGN KEY `FK_test_materials_users_CustodianId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_project_followups' AND CONSTRAINT_NAME='FK_test_project_followups_users_FilledById') THEN ALTER TABLE `test_project_followups` DROP FOREIGN KEY `FK_test_project_followups_users_FilledById`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='test_projects' AND CONSTRAINT_NAME='FK_test_projects_users_OwnerId') THEN ALTER TABLE `test_projects` DROP FOREIGN KEY `FK_test_projects_users_OwnerId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='users' AND CONSTRAINT_NAME='FK_users_departments_DepartmentId') THEN ALTER TABLE `users` DROP FOREIGN KEY `FK_users_departments_DepartmentId`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='users' AND CONSTRAINT_NAME='FK_users_users_SupervisorId') THEN ALTER TABLE `users` DROP FOREIGN KEY `FK_users_users_SupervisorId`; END IF;

                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND INDEX_NAME='IX_users_DepartmentId') THEN DROP INDEX `IX_users_DepartmentId` ON `users`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND INDEX_NAME='IX_users_SupervisorId') THEN DROP INDEX `IX_users_SupervisorId` ON `users`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_project_followups' AND INDEX_NAME='IX_test_project_followups_FilledById') THEN DROP INDEX `IX_test_project_followups_FilledById` ON `test_project_followups`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND INDEX_NAME='IX_test_materials_CustodianId') THEN DROP INDEX `IX_test_materials_CustodianId` ON `test_materials`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_materials' AND INDEX_NAME='IX_test_materials_LocationId') THEN DROP INDEX `IX_test_materials_LocationId` ON `test_materials`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='roles' AND INDEX_NAME='IX_roles_Name') THEN DROP INDEX `IX_roles_Name` ON `roles`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='locations' AND INDEX_NAME='IX_locations_Name') THEN DROP INDEX `IX_locations_Name` ON `locations`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='departments' AND INDEX_NAME='IX_departments_ManagerId') THEN DROP INDEX `IX_departments_ManagerId` ON `departments`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='departments' AND INDEX_NAME='IX_departments_Name') THEN DROP INDEX `IX_departments_Name` ON `departments`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='assets' AND INDEX_NAME='IX_assets_CustodianId') THEN DROP INDEX `IX_assets_CustodianId` ON `assets`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='assets' AND INDEX_NAME='IX_assets_LocationId') THEN DROP INDEX `IX_assets_LocationId` ON `assets`; END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='test_projects' AND COLUMN_NAME='RowVersion') THEN ALTER TABLE `test_projects` DROP COLUMN `RowVersion`; END IF;
                END
                """);
            migrationBuilder.Sql("CALL `reset_partial_relational_hardening`()");
            migrationBuilder.Sql("DROP PROCEDURE `reset_partial_relational_hardening`");

            migrationBuilder.AddColumn<uint>(
                name: "RowVersion",
                table: "test_projects",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_users_DepartmentId",
                table: "users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_users_SupervisorId",
                table: "users",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_test_project_followups_FilledById",
                table: "test_project_followups",
                column: "FilledById");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_CustodianId",
                table: "test_materials",
                column: "CustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_test_materials_LocationId",
                table: "test_materials",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_Name",
                table: "locations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_departments_ManagerId",
                table: "departments",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_departments_Name",
                table: "departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_CustodianId",
                table: "assets",
                column: "CustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_LocationId",
                table: "assets",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_approval_flows_assets_AssetId",
                table: "approval_flows",
                column: "AssetId",
                principalTable: "assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_approval_flows_users_ApplicantId",
                table: "approval_flows",
                column: "ApplicantId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_approval_flows_users_TransfereeId",
                table: "approval_flows",
                column: "TransfereeId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_approval_flows_workflows_WorkflowId",
                table: "approval_flows",
                column: "WorkflowId",
                principalTable: "workflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_categories_asset_categories_ParentId",
                table: "asset_categories",
                column: "ParentId",
                principalTable: "asset_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_asset_categories_CategoryId",
                table: "assets",
                column: "CategoryId",
                principalTable: "asset_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_departments_DepartmentId",
                table: "assets",
                column: "DepartmentId",
                principalTable: "departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_locations_LocationId",
                table: "assets",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_users_CustodianId",
                table: "assets",
                column: "CustodianId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_UserId",
                table: "audit_logs",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_departments_departments_ParentId",
                table: "departments",
                column: "ParentId",
                principalTable: "departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_departments_users_ManagerId",
                table: "departments",
                column: "ManagerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_records_approval_flows_FlowId",
                table: "flow_records",
                column: "FlowId",
                principalTable: "approval_flows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_material_flow_records_material_flows_FlowId",
                table: "material_flow_records",
                column: "FlowId",
                principalTable: "material_flows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_menus_menus_ParentId",
                table: "menus",
                column: "ParentId",
                principalTable: "menus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_materials_departments_DepartmentId",
                table: "test_materials",
                column: "DepartmentId",
                principalTable: "departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_materials_locations_LocationId",
                table: "test_materials",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_materials_users_CustodianId",
                table: "test_materials",
                column: "CustodianId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_project_followups_users_FilledById",
                table: "test_project_followups",
                column: "FilledById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_test_projects_users_OwnerId",
                table: "test_projects",
                column: "OwnerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_departments_DepartmentId",
                table: "users",
                column: "DepartmentId",
                principalTable: "departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_SupervisorId",
                table: "users",
                column: "SupervisorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_approval_flows_assets_AssetId",
                table: "approval_flows");

            migrationBuilder.DropForeignKey(
                name: "FK_approval_flows_users_ApplicantId",
                table: "approval_flows");

            migrationBuilder.DropForeignKey(
                name: "FK_approval_flows_users_TransfereeId",
                table: "approval_flows");

            migrationBuilder.DropForeignKey(
                name: "FK_approval_flows_workflows_WorkflowId",
                table: "approval_flows");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_categories_asset_categories_ParentId",
                table: "asset_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_asset_categories_CategoryId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_departments_DepartmentId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_locations_LocationId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_users_CustodianId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_UserId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_departments_departments_ParentId",
                table: "departments");

            migrationBuilder.DropForeignKey(
                name: "FK_departments_users_ManagerId",
                table: "departments");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_records_approval_flows_FlowId",
                table: "flow_records");

            migrationBuilder.DropForeignKey(
                name: "FK_material_flow_records_material_flows_FlowId",
                table: "material_flow_records");

            migrationBuilder.DropForeignKey(
                name: "FK_menus_menus_ParentId",
                table: "menus");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_test_materials_departments_DepartmentId",
                table: "test_materials");

            migrationBuilder.DropForeignKey(
                name: "FK_test_materials_locations_LocationId",
                table: "test_materials");

            migrationBuilder.DropForeignKey(
                name: "FK_test_materials_users_CustodianId",
                table: "test_materials");

            migrationBuilder.DropForeignKey(
                name: "FK_test_project_followups_users_FilledById",
                table: "test_project_followups");

            migrationBuilder.DropForeignKey(
                name: "FK_test_projects_users_OwnerId",
                table: "test_projects");

            migrationBuilder.DropForeignKey(
                name: "FK_users_departments_DepartmentId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_users_SupervisorId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_DepartmentId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_SupervisorId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_test_project_followups_FilledById",
                table: "test_project_followups");

            migrationBuilder.DropIndex(
                name: "IX_test_materials_CustodianId",
                table: "test_materials");

            migrationBuilder.DropIndex(
                name: "IX_test_materials_LocationId",
                table: "test_materials");

            migrationBuilder.DropIndex(
                name: "IX_roles_Name",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_locations_Name",
                table: "locations");

            migrationBuilder.DropIndex(
                name: "IX_departments_ManagerId",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "IX_departments_Name",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "IX_assets_CustodianId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_LocationId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "test_projects");

        }
    }
}
