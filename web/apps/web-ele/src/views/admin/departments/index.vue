<script lang="ts" setup>
import DepartmentFormDialog from './DepartmentFormDialog.vue';
import DepartmentHeader from './DepartmentHeader.vue';
import DepartmentTable from './DepartmentTable.vue';
import { useDepartmentPage } from './use-department-page';

defineOptions({ name: 'AdminDepartments' });

const {
  canCreateChild,
  departmentActionAccess,
  departments,
  dialogVisible,
  editingId,
  formModel,
  loading,
  onPageSizeChange,
  onParentChange,
  openCreate,
  openEdit,
  page,
  pageSize,
  pageSizeOptions,
  pagedDepartments,
  remove,
  save,
  saving,
  searchUsers,
  selectableOrganizationLevels,
  userOptions,
  userOptionsLoading,
} = useDepartmentPage();
</script>

<template>
  <re-page>
    <div class="page-container page-fit">
      <DepartmentHeader
        :access="departmentActionAccess"
        @create="openCreate()"
      />
      <DepartmentTable
        v-model:page="page"
        v-model:page-size="pageSize"
        :access="departmentActionAccess"
        :can-create-child="canCreateChild"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :paged-departments="pagedDepartments"
        :total="departments.length"
        @create-child="openCreate"
        @edit="openEdit"
        @page-size-change="onPageSizeChange"
        @remove="remove"
      />
      <DepartmentFormDialog
        v-model:form="formModel"
        v-model:visible="dialogVisible"
        :departments="departments"
        :editing="!!editingId"
        :saving="saving"
        :selectable-organization-levels="selectableOrganizationLevels"
        :user-options="userOptions"
        :user-options-loading="userOptionsLoading"
        @parent-change="onParentChange"
        @save="save"
        @user-search="searchUsers"
      />
    </div>
  </re-page>
</template>
