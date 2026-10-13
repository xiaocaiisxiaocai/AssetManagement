import type { MenuDto } from '#/api/role';

import { ElMessage } from 'element-plus';

import { setRoleAccessApi } from '#/api/role';

import { mergeMenuTreeSelection } from './menu-tree-selection';

export function bindRoleAccessActions(page: Record<string, any>) {
  const {
    saving,
    accessDialogVisible,
    activePermissionGroupKey,
    accessForm,
    pageMenuTree,
    requiredPermissionIdSet,
    activePermissionGroup,
    loadData,
    syncRequiredPermissions,
  } = page;

  function selectPermissionGroup(key: string) {
    activePermissionGroupKey.value = key;
  }

  function selectCurrentModulePermissions() {
    const group = activePermissionGroup.value;
    if (!group) return;
    const selected = new Set(accessForm.selectedPermissions);
    group.permissions.forEach((perm: { id: number }) => selected.add(perm.id));
    accessForm.selectedPermissions = [...selected];
  }

  function clearCurrentModulePermissions() {
    const group = activePermissionGroup.value;
    if (!group) return;
    const ids = new Set(
      group.permissions.map((perm: { id: number }) => perm.id),
    );
    accessForm.selectedPermissions = accessForm.selectedPermissions.filter(
      (id: number) => !ids.has(id) || requiredPermissionIdSet.value.has(id),
    );
  }

  function syncMenuSelection(payload: {
    checkedKeys: number[];
    halfCheckedKeys: number[];
  }) {
    accessForm.selectedMenus = mergeMenuTreeSelection(
      payload.checkedKeys,
      payload.halfCheckedKeys,
    );
    syncRequiredPermissions();
  }

  async function saveAccess() {
    saving.value = true;
    try {
      await setRoleAccessApi(
        accessForm.roleId,
        accessForm.selectedPermissions,
        accessForm.selectedMenus,
      );
      ElMessage.success('角色授权已保存');
      accessDialogVisible.value = false;
      await loadData();
    } finally {
      saving.value = false;
    }
  }

  function leafMenuIds(selectedIds: number[]) {
    const selected = new Set(selectedIds);
    return flattenMenus(pageMenuTree.value)
      .filter((menu) => selected.has(menu.id) && !menu.children?.length)
      .map((menu) => menu.id);
  }

  function flattenMenus(items: MenuDto[]): MenuDto[] {
    return items.flatMap((item) => [
      item,
      ...flattenMenus(item.children ?? []),
    ]);
  }

  return {
    selectPermissionGroup,
    selectCurrentModulePermissions,
    clearCurrentModulePermissions,
    syncMenuSelection,
    saveAccess,
    leafMenuIds,
    flattenMenus,
  };
}
