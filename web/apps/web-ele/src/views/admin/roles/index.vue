<script lang="ts" setup>
import { runHandled } from '#/utils/handled-promise';

import RoleAccessDialog from './RoleAccessDialog.vue';
import RoleFilter from './RoleFilter.vue';
import RoleFormDialog from './RoleFormDialog.vue';
import RoleHeader from './RoleHeader.vue';
import RoleTable from './RoleTable.vue';
import { useRoleAdmin } from './use-role-admin';

defineOptions({ name: 'AdminRoles' });

const {
  roleActionAccess,
  loading,
  saving,
  dialogVisible,
  accessDialogVisible,
  editingId,
  roles,
  permissions,
  total,
  permissionKeyword,
  showSelectedOnly,
  pageSizeOptions,
  queryModel,
  formModel,
  accessForm,
  selectedPermissionsModel,
  canConfigureAccess,
  pageMenuTree,
  requiredPermissionIdSet,
  permissionGroups,
  filteredPermissions,
  activePermissionGroupKeyValue,
  activePermissionGroupLabel,
  selectPermissionGroup,
  selectCurrentModulePermissions,
  clearCurrentModulePermissions,
  loadData,
  openCreate,
  openEdit,
  save,
  openAccessDialog,
  syncMenuSelection,
  saveAccess,
  leafMenuIds,
  remove,
  search,
  reset,
} = useRoleAdmin();
</script>

<template>
  <re-page>
    <div class="role-page page-fit">
      <RoleHeader :access="roleActionAccess" @create="openCreate" />
      <RoleFilter v-model:query="queryModel" @reset="reset" @search="search" />
      <RoleTable
        v-model:query="queryModel"
        :access="roleActionAccess"
        :can-configure-access="canConfigureAccess"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :roles="roles"
        :total="total"
        @access="openAccessDialog"
        @edit="openEdit"
        @page-change="loadData"
        @remove="(row) => runHandled(remove(row))"
        @search="search"
      />
      <RoleFormDialog
        v-model:form="formModel"
        v-model:visible="dialogVisible"
        :editing="!!editingId"
        :saving="saving"
        @save="runHandled(save())"
      />
      <RoleAccessDialog
        v-model:permission-keyword="permissionKeyword"
        v-model:selected-permissions="selectedPermissionsModel"
        v-model:show-selected-only="showSelectedOnly"
        v-model:visible="accessDialogVisible"
        :active-permission-group-key-value="activePermissionGroupKeyValue"
        :active-permission-group-label="activePermissionGroupLabel"
        :checked-leaf-menu-ids="leafMenuIds(accessForm.selectedMenus)"
        :filtered-permissions="filteredPermissions"
        :page-menu-tree="pageMenuTree"
        :permission-count="permissions.length"
        :permission-groups="permissionGroups"
        :required-permission-ids="[...requiredPermissionIdSet]"
        :role-name="accessForm.roleName"
        :saving="saving"
        :selected-menu-count="accessForm.selectedMenus.length"
        @clear-current="clearCurrentModulePermissions"
        @save="runHandled(saveAccess())"
        @select-current="selectCurrentModulePermissions"
        @select-group="selectPermissionGroup"
        @tree-check="syncMenuSelection"
      />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
