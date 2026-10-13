import type { RoleDto } from '#/api/role';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createRoleApi,
  deleteRoleApi,
  getRoleAccessOptionsApi,
  getRoleListApi,
  updateRoleApi,
} from '#/api/role';
import { runHandled } from '#/utils/handled-promise';
import { sortBuiltInMenus } from '#/utils/menu-order';

import { collectRequiredPermissionIds } from './menu-tree-selection';

export function bindRoleRecordActions(page: Record<string, any>) {
  const {
    listRequestGuard,
    loading,
    saving,
    dialogVisible,
    accessDialogVisible,
    editingId,
    roles,
    permissions,
    menus,
    total,
    activePermissionGroupKey,
    permissionKeyword,
    showSelectedOnly,
    query,
    form,
    accessForm,
    canConfigureAccess,
    pageMenuTree,
    permissionGroups,
  } = page;

  async function loadData() {
    const requestGeneration = listRequestGuard.next();
    loading.value = true;
    try {
      const result = await getRoleListApi(
        query.keyword,
        query.page,
        query.pageSize,
      );
      if (!listRequestGuard.isLatest(requestGeneration)) return;
      roles.value = result.items;
      total.value = result.total;
    } finally {
      if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
    }
  }

  async function loadPermissionsAndMenus() {
    if (!canConfigureAccess.value) return;
    const options = await getRoleAccessOptionsApi();
    permissions.value = options.permissions;
    menus.value = sortBuiltInMenus(options.menus);
  }

  function openCreate() {
    editingId.value = null;
    Object.assign(form, {
      code: '',
      name: '',
      isActive: true,
    });
    dialogVisible.value = true;
  }

  function openEdit(row: RoleDto) {
    editingId.value = row.id;
    Object.assign(form, {
      code: row.code,
      name: row.name,
      isActive: row.isActive,
    });
    dialogVisible.value = true;
  }

  async function save() {
    if (!form.name.trim()) {
      ElMessage.warning('请填写角色名称');
      return;
    }
    if (!editingId.value && !form.code.trim()) {
      ElMessage.warning('新增角色需要填写角色编码');
      return;
    }

    saving.value = true;
    try {
      const payload = {
        name: form.name,
        isActive: form.isActive,
      };

      if (editingId.value) {
        await updateRoleApi(editingId.value, payload);
      } else {
        const created = await createRoleApi({
          code: form.code,
          ...payload,
        });
        ElMessage.success('角色已创建，请继续配置授权');
        dialogVisible.value = false;
        query.page = 1;
        await loadData();
        if (canConfigureAccess.value) {
          await openAccessDialog(created);
        }
        return;
      }
      ElMessage.success('保存成功');
      dialogVisible.value = false;
      query.page = 1;
      await loadData();
    } finally {
      saving.value = false;
    }
  }

  async function openAccessDialog(row: RoleDto) {
    accessForm.roleId = row.id;
    accessForm.roleName = row.name;
    accessForm.selectedPermissions = [...(row.permissionIds ?? [])];
    accessForm.selectedMenus = [...(row.menuIds ?? [])];
    permissionKeyword.value = '';
    showSelectedOnly.value = false;
    activePermissionGroupKey.value = permissionGroups.value[0]?.key ?? '';
    syncRequiredPermissions();
    accessDialogVisible.value = true;
  }

  function syncRequiredPermissions() {
    const requiredIds = collectRequiredPermissionIds(
      pageMenuTree.value,
      permissions.value,
      accessForm.selectedMenus,
    );
    accessForm.selectedPermissions = [
      ...new Set([...accessForm.selectedPermissions, ...requiredIds]),
    ];
  }

  async function remove(row: RoleDto) {
    try {
      await ElMessageBox.confirm(`确认删除角色「${row.name}」？`, '删除确认', {
        type: 'warning',
      });
    } catch {
      return;
    }
    await deleteRoleApi(row.id);
    ElMessage.success('删除成功');
    await loadData();
  }

  function search() {
    query.page = 1;
    runHandled(loadData());
  }

  function reset() {
    query.keyword = '';
    query.page = 1;
    runHandled(loadData());
  }

  return {
    loadData,
    loadPermissionsAndMenus,
    openCreate,
    openEdit,
    save,
    openAccessDialog,
    syncRequiredPermissions,
    remove,
    search,
    reset,
  };
}
