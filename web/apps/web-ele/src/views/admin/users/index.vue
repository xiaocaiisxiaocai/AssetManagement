<script lang="ts" setup>
import { useUserAdmin } from './use-user-admin';
import UserFilter from './UserFilter.vue';
import UserFormDialog from './UserFormDialog.vue';
import UserHeader from './UserHeader.vue';
import UserImportDialog from './UserImportDialog.vue';
import UserTable from './UserTable.vue';

defineOptions({ name: 'AdminUsers' });

const {
  userActionAccess,
  loading,
  saving,
  dialogVisible,
  importDialogVisible,
  editingId,
  users,
  supervisorOptions,
  supervisorOptionsLoading,
  importing,
  importRows,
  selectedImportFile,
  roles,
  total,
  pageSizeOptions,
  queryModel,
  formModel,
  departmentOptions,
  loadData,
  openCreate,
  openEdit,
  searchSupervisors,
  onDepartmentChange,
  save,
  resetPassword,
  toggleStatus,
  remove,
  downloadImportTemplate,
  openImport,
  onImportFileChange,
  confirmImportUsers,
  search,
  reset,
} = useUserAdmin();
</script>

<template>
  <re-page>
    <div class="user-page page-fit">
      <UserHeader
        :access="userActionAccess"
        @create="openCreate"
        @import="openImport"
      />
      <UserFilter
        v-model:query="queryModel"
        @reset="reset"
        @search="search"
      />
      <UserTable
        v-model:query="queryModel"
        :access="userActionAccess"
        :department-options="departmentOptions"
        :roles="roles"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :total="total"
        :users="users"
        @edit="openEdit"
        @page-change="loadData"
        @remove="remove"
        @reset-password="resetPassword"
        @search="search"
        @toggle-status="toggleStatus"
      />
      <UserFormDialog
        v-model:form="formModel"
        v-model:visible="dialogVisible"
        :can-assign-role="userActionAccess.canAssignRole"
        :department-options="departmentOptions"
        :editing="!!editingId"
        :roles="roles"
        :saving="saving"
        :supervisor-options="supervisorOptions"
        :supervisor-options-loading="supervisorOptionsLoading"
        @department-change="onDepartmentChange"
        @save="save"
        @supervisor-search="searchSupervisors"
      />
      <UserImportDialog
        v-model:visible="importDialogVisible"
        :importing="importing"
        :rows="importRows"
        :selected-file="selectedImportFile"
        @confirm="confirmImportUsers"
        @download-template="downloadImportTemplate"
        @file-change="onImportFileChange"
      />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
