import type { UserFormState, UserListQuery } from './user-form';

import type { DepartmentOptionNode } from '#/api/base-data';
import type { RoleDto } from '#/api/role';
import type { UserDto, UserImportRow, UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';

import { flattenActiveDepartments } from '#/utils/department-options';
import { createImportValidationSession } from '#/utils/import-validation-session';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildUserActionAccess } from '#/views/permissions/action-access';

import { bindUserAccountActions } from './user-account-actions';
import { bindUserImportActions } from './user-import-actions';

export function useUserAdmin() {
  const listRequestGuard = createLatestRequestGuard();

  const { hasAccessByCodes } = useAccess();
  const userActionAccess = computed(() =>
    buildUserActionAccess(hasAccessByCodes),
  );
  const loading = ref(false);
  const saving = ref(false);
  const dialogVisible = ref(false);
  const importDialogVisible = ref(false);
  const editingId = ref<null | number>(null);
  const users = ref<UserDto[]>([]);
  const supervisorOptions = ref<UserOptionDto[]>([]);
  const supervisorOptionsLoading = ref(false);
  const supervisorOptionsRequestGuard = createLatestRequestGuard();
  const importValidation = createImportValidationSession<UserImportRow>();
  const importing = importValidation.loading;
  const importRows = importValidation.rows;
  const selectedImportFile = importValidation.selectedFile;
  const roles = ref<RoleDto[]>([]);
  const departments = ref<DepartmentOptionNode[]>([]);
  const total = ref(0);
  const pageSizeOptions = ref(createPageSizeOptions(20));

  const query = reactive<UserListQuery>({
    departmentId: undefined,
    keyword: '',
    page: 1,
    pageSize: 20,
    roleId: undefined,
  });

  const form = reactive<UserFormState>({
    employeeNo: '',
    name: '',
    phone: '',
    email: '',
    departmentId: undefined,
    roleId: undefined,
    roleName: '',
    supervisorId: undefined,
  });
  const queryModel = reactiveObjectModel(query);
  const formModel = reactiveObjectModel(form);

  const departmentOptions = computed(() =>
    flattenActiveDepartments(departments.value),
  );

  watch(importDialogVisible, (opened) => {
    if (!opened) importValidation.reset();
  });

  const bindUserAccountActionsResult = bindUserAccountActions({
    listRequestGuard,
    userActionAccess,
    loading,
    saving,
    dialogVisible,
    editingId,
    users,
    supervisorOptions,
    supervisorOptionsLoading,
    supervisorOptionsRequestGuard,
    roles,
    departments,
    total,
    query,
    form,
    departmentOptions,
  });
  const {
    loadRoles,
    loadDepartments,
    loadData,
    openCreate,
    openEdit,
    searchSupervisors,
    onDepartmentChange,
    save,
    resetPassword,
    toggleStatus,
    remove,
    search,
    reset,
  } = bindUserAccountActionsResult;

  const bindUserImportActionsResult = bindUserImportActions({
    importDialogVisible,
    importValidation,
    importing,
    importRows,
    selectedImportFile,
    query,
    loadData,
  });
  const {
    downloadBlob,
    downloadImportTemplate,
    openImport,
    onImportFileChange,
    confirmImportUsers,
  } = bindUserImportActionsResult;

  onMounted(async () => {
    query.pageSize = await getDefaultPageSize();
    pageSizeOptions.value = createPageSizeOptions(query.pageSize);
    await Promise.all([loadRoles(), loadDepartments()]);
    await loadData();
  });

  return {
    listRequestGuard,
    userActionAccess,
    loading,
    saving,
    dialogVisible,
    importDialogVisible,
    editingId,
    users,
    supervisorOptions,
    supervisorOptionsLoading,
    supervisorOptionsRequestGuard,
    importValidation,
    importing,
    importRows,
    selectedImportFile,
    roles,
    departments,
    total,
    pageSizeOptions,
    query,
    form,
    queryModel,
    formModel,
    departmentOptions,
    loadRoles,
    loadDepartments,
    loadData,
    openCreate,
    openEdit,
    searchSupervisors,
    onDepartmentChange,
    save,
    resetPassword,
    toggleStatus,
    remove,
    downloadBlob,
    downloadImportTemplate,
    openImport,
    onImportFileChange,
    confirmImportUsers,
    search,
    reset,
  };
}
