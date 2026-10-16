import type { UserDto } from '#/api/user';

import { ElMessage, ElMessageBox } from 'element-plus';

import { getDepartmentOptionsApi } from '#/api/base-data';
import { getRoleListApi } from '#/api/role';
import {
  createUserApi,
  deleteUserApi,
  getApproverOptionsApi,
  getUserListApi,
  resetUserPasswordApi,
  toggleUserStatusApi,
  updateUserApi,
} from '#/api/user';
import { runHandled } from '#/utils/handled-promise';
import {
  mergeSelectedUserOption,
  replaceUserOptions,
} from '#/utils/user-options';

import {
  buildUserPayload,
  resolveDefaultEmployeeRoleId,
  resolveDepartmentSupervisor,
  userToForm,
} from './user-form';

export function bindUserAccountActions(page: Record<string, any>) {
  const {
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
  } = page;

  async function loadRoles() {
    if (!userActionAccess.value.canViewRoles) return;
    const result = await getRoleListApi(undefined, 1, 100);
    roles.value = result.items;
    if (
      dialogVisible.value &&
      editingId.value === null &&
      form.roleId === undefined
    ) {
      form.roleId = resolveDefaultEmployeeRoleId(roles.value);
    }
  }

  async function loadDepartments() {
    if (!userActionAccess.value.canCreate && !userActionAccess.value.canEdit)
      return;
    departments.value = await getDepartmentOptionsApi();
  }

  async function loadData() {
    const requestGeneration = listRequestGuard.next();
    loading.value = true;
    try {
      const result = await getUserListApi(
        query.keyword,
        query.page,
        query.pageSize,
        undefined,
        undefined,
        query.departmentIds,
        query.roleIds,
      );
      if (!listRequestGuard.isLatest(requestGeneration)) return;
      users.value = result.items;
      total.value = result.total;
    } finally {
      if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
    }
  }

  function openCreate() {
    editingId.value = null;
    Object.assign(form, {
      employeeNo: '',
      name: '',
      phone: '',
      email: '',
      departmentId: undefined,
      roleId: resolveDefaultEmployeeRoleId(roles.value),
      roleName: userActionAccess.value.canAssignRole ? '' : '普通员工',
      supervisorId: undefined,
    });
    dialogVisible.value = true;
    runHandled(searchSupervisors(''));
  }

  function openEdit(row: UserDto) {
    editingId.value = row.id;
    Object.assign(form, userToForm(row));
    supervisorOptions.value = mergeSelectedUserOption(supervisorOptions.value, {
      id: row.supervisorId,
      name: row.supervisorName,
    });
    dialogVisible.value = true;
    runHandled(searchSupervisors(''));
  }

  async function searchSupervisors(keyword = '') {
    const generation = supervisorOptionsRequestGuard.next();
    supervisorOptionsLoading.value = true;
    try {
      const result = await getApproverOptionsApi(keyword);
      if (!supervisorOptionsRequestGuard.isLatest(generation)) return;
      supervisorOptions.value = replaceUserOptions(
        supervisorOptions.value,
        result.filter((user) => user.id !== editingId.value),
        [form.supervisorId],
      );
    } catch {
      // 请求层已提示，保留现有选项。
    } finally {
      if (supervisorOptionsRequestGuard.isLatest(generation))
        supervisorOptionsLoading.value = false;
    }
  }

  function onDepartmentChange(departmentId?: number) {
    const selection = resolveDepartmentSupervisor(
      departmentOptions.value,
      departmentId,
      editingId.value,
    );
    form.supervisorId = selection.supervisorId;
    supervisorOptions.value = mergeSelectedUserOption(supervisorOptions.value, {
      id: selection.supervisorId,
      name: selection.supervisorName,
    });
  }

  async function save() {
    if (!form.name.trim()) {
      ElMessage.warning('请填写用户名称');
      return;
    }
    if (!editingId.value && !form.employeeNo.trim()) {
      ElMessage.warning('新增用户需要填写工号');
      return;
    }
    if (userActionAccess.value.canAssignRole && !form.roleId) {
      ElMessage.warning('请选择角色');
      return;
    }

    saving.value = true;
    try {
      const payload = buildUserPayload(form);

      await (editingId.value
        ? updateUserApi(editingId.value, payload)
        : createUserApi({
            employeeNo: form.employeeNo,
            ...payload,
          }));
      ElMessage.success('保存成功');
      dialogVisible.value = false;
      query.page = 1;
      await loadData();
    } finally {
      saving.value = false;
    }
  }

  async function resetPassword(row: UserDto) {
    try {
      await ElMessageBox.confirm(
        `确认重置用户「${row.name}」的密码？重置后默认密码为 123456`,
        '重置密码',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    await resetUserPasswordApi(row.id);
    ElMessage.success('密码已重置');
  }

  async function toggleStatus(row: UserDto) {
    const action = row.isActive ? '禁用' : '启用';
    try {
      await ElMessageBox.confirm(`确认${action}用户「${row.name}」？`, '确认', {
        type: 'warning',
      });
    } catch {
      return;
    }
    await toggleUserStatusApi(row.id, !row.isActive);
    ElMessage.success(`${action}成功`);
    await loadData();
  }

  async function remove(row: UserDto) {
    try {
      await ElMessageBox.confirm(`确认删除用户「${row.name}」？`, '删除确认', {
        type: 'warning',
      });
    } catch {
      return;
    }
    await deleteUserApi(row.id);
    ElMessage.success('删除成功');
    await loadData();
  }

  function search() {
    query.page = 1;
    runHandled(loadData());
  }

  function reset() {
    query.departmentIds = [];
    query.keyword = '';
    query.page = 1;
    query.roleIds = [];
    runHandled(loadData());
  }

  return {
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
  };
}
