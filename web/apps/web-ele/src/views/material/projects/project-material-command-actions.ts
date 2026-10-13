import type { MaterialItem } from '#/api/material';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  deleteMaterialApi,
  getMaterialDetailApi,
  purgeMaterialApi,
  restoreMaterialApi,
  returnMaterialApi,
} from '#/api/material';
import { runHandled } from '#/utils/handled-promise';
import { mergeSelectedUserOption } from '#/utils/user-options';

import { refreshProjectAfterMaterialChange } from './project-workspace-rules';

export function bindProjectMaterialCommandActions(
  page: Record<string, any>,
  deps: {
    loadBaseOptions: () => Promise<void>;
    loadData: () => Promise<void>;
    loadProjectFlows: (projectId?: number) => Promise<void>;
    loadProjectMaterials: (projectId?: number) => Promise<void>;
    loadUsers: () => Promise<void>;
    searchUsers: (
      keyword?: string,
      keepIds?: Array<null | number | undefined>,
    ) => Promise<void>;
  },
) {
  const {
    loadBaseOptions,
    loadData,
    loadProjectFlows,
    loadProjectMaterials,
    loadUsers,
    searchUsers,
  } = deps;
  const {
    currentProject,
    isCurrentProjectReadOnly,
    users,
    materialFormVisible,
    editingMaterial,
    materialDetailVisible,
    materialDetailLoading,
    materialDetail,
    materialDetailRequestGuard,
    transferVisible,
    transferMaterial,
  } = page;

  function loadMaterialFormOptions(userKeyword = '') {
    runHandled(
      Promise.all([
        userKeyword ? searchUsers(userKeyword) : loadUsers(),
        loadBaseOptions(),
      ]),
    );
  }

  function openCreateMaterial() {
    if (isCurrentProjectReadOnly.value) return;
    editingMaterial.value = null;
    loadMaterialFormOptions(currentProject.value?.ownerName ?? '');
    materialFormVisible.value = true;
  }

  function onMaterialRowCommand(command: number | string, row: MaterialItem) {
    if (isCurrentProjectReadOnly.value) return;
    switch (command) {
      case 'delete': {
        runHandled(removeMaterial(row));
        break;
      }
      case 'purge': {
        runHandled(purgeMaterial(row));
        break;
      }
      case 'restore': {
        runHandled(restoreMaterial(row));
        break;
      }
      case 'return': {
        runHandled(onReturnMaterial(row));
        break;
      }
      case 'transfer': {
        openTransfer(row);
        break;
      }
      // no default
    }
  }

  function openEditMaterial(row: MaterialItem) {
    if (isCurrentProjectReadOnly.value) return;
    users.value = mergeSelectedUserOption(users.value, {
      id: row.custodianId,
      name: row.custodianName,
    });
    editingMaterial.value = row;
    loadMaterialFormOptions();
    materialFormVisible.value = true;
  }

  async function openMaterialDetail(row: MaterialItem) {
    const requestGeneration = materialDetailRequestGuard.next();
    materialDetailVisible.value = true;
    materialDetailLoading.value = true;
    materialDetail.value = null;
    try {
      const response = await getMaterialDetailApi(row.id);
      if (
        materialDetailRequestGuard.isLatest(requestGeneration) &&
        materialDetailVisible.value
      ) {
        materialDetail.value = response;
      }
    } finally {
      if (materialDetailRequestGuard.isLatest(requestGeneration)) {
        materialDetailLoading.value = false;
      }
    }
  }

  function openTransfer(row: MaterialItem) {
    if (isCurrentProjectReadOnly.value) return;
    transferMaterial.value = row;
    transferVisible.value = true;
  }

  async function onReturnMaterial(row: MaterialItem) {
    try {
      await ElMessageBox.confirm(
        `确认将料件「${row.name}」标记为已退回厂商？`,
        '退回厂商',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await returnMaterialApi(row.id);
      ElMessage.success('已标记为退回厂商');
      await afterMaterialChanged();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function removeMaterial(row: MaterialItem) {
    try {
      await ElMessageBox.confirm(
        `确认删除料件「${row.name}」？删除后仍显示在清单中，可由管理员彻底删除。`,
        '删除确认',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await deleteMaterialApi(row.id);
      ElMessage.success('已删除');
      await afterMaterialChanged();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function restoreMaterial(row: MaterialItem) {
    try {
      await ElMessageBox.confirm(
        `确认撤销删除料件「${row.name}」？`,
        '撤销删除',
        {
          type: 'warning',
        },
      );
    } catch {
      return;
    }
    try {
      await restoreMaterialApi(row.id);
      ElMessage.success('已恢复');
      await afterMaterialChanged();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function purgeMaterial(row: MaterialItem) {
    try {
      await ElMessageBox.confirm(
        `彻底删除料件「${row.name}」后不可恢复，确认继续？`,
        '彻底删除确认',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await purgeMaterialApi(row.id);
      ElMessage.success('已彻底删除');
      await afterMaterialChanged();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function afterMaterialChanged() {
    await refreshProjectAfterMaterialChange(
      loadProjectMaterials,
      loadProjectFlows,
      loadData,
    );
  }

  return {
    loadMaterialFormOptions,
    openCreateMaterial,
    onMaterialRowCommand,
    openEditMaterial,
    openMaterialDetail,
    openTransfer,
    onReturnMaterial,
    removeMaterial,
    restoreMaterial,
    purgeMaterial,
    afterMaterialChanged,
  };
}
