import type { AssetItem } from '#/api/asset';

import { useDebounceFn } from '@vueuse/core';
import { ElMessage, ElMessageBox } from 'element-plus';

import {
  deleteAssetApi,
  getAssetDetailApi,
  purgeAssetApi,
  restoreAssetApi,
} from '#/api/asset';
import { runHandled } from '#/utils/handled-promise';
import { mergeSelectedUserOption } from '#/utils/user-options';

import { canBorrowAvailableAsset } from './asset-row-actions';

export function bindAssetRowCommands(page: Record<string, any>) {
  const {
    currentUserId,
    deletingAssetIds,
    dialogVisible,
    borrowDialogVisible,
    transferDialogVisible,
    editingAsset,
    formDefaultCategoryId,
    selectedCategoryId,
    assets,
    total,
    users,
    currentAssetForAction,
    detailVisible,
    detailLoading,
    detailRequestGuard,
    detail,
    query,
    loadData,
    loadHierarchyAssetCounts,
  } = page;

  function openCreate(categoryId?: number) {
    editingAsset.value = null;
    formDefaultCategoryId.value =
      categoryId ?? selectedCategoryId.value ?? query.categoryId ?? 0;
    dialogVisible.value = true;
  }

  function openEdit(row: AssetItem) {
    users.value = mergeSelectedUserOption(users.value, {
      id: row.custodianId,
      name: row.custodianName,
    });
    editingAsset.value = row;
    dialogVisible.value = true;
  }

  async function openDetail(row: AssetItem) {
    const requestGeneration = detailRequestGuard.next();
    detailVisible.value = true;
    detailLoading.value = true;
    detail.value = null;
    try {
      const response = await getAssetDetailApi(row.id);
      if (
        detailRequestGuard.isLatest(requestGeneration) &&
        detailVisible.value
      ) {
        detail.value = response;
      }
    } finally {
      if (detailRequestGuard.isLatest(requestGeneration)) {
        detailLoading.value = false;
      }
    }
  }

  function onSaved() {
    runHandled(Promise.all([loadData(), loadHierarchyAssetCounts()]));
  }

  function onImported() {
    runHandled(Promise.all([loadData(), loadHierarchyAssetCounts()]));
  }

  async function remove(row: AssetItem) {
    if (deletingAssetIds.value.includes(row.id)) {
      return;
    }
    try {
      await ElMessageBox.confirm(
        `确认删除资产「${row.name}」？删除后仍显示在清单中，可由管理员彻底删除。`,
        '删除确认',
        {
          type: 'warning',
        },
      );
    } catch {
      return;
    }
    deletingAssetIds.value = [...deletingAssetIds.value, row.id];
    try {
      await deleteAssetApi(row.id);
      if (query.deleteStatus === 'active') {
        assets.value = assets.value.filter(
          (item: AssetItem) => item.id !== row.id,
        );
        total.value = Math.max(total.value - 1, 0);
      }
      ElMessage.success('已删除');
      await Promise.all([loadData(), loadHierarchyAssetCounts()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      deletingAssetIds.value = deletingAssetIds.value.filter(
        (id: number) => id !== row.id,
      );
    }
  }

  async function purge(row: AssetItem) {
    try {
      await ElMessageBox.confirm(
        `彻底删除资产「${row.name}」后不可恢复，确认继续？`,
        '彻底删除确认',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await purgeAssetApi(row.id);
      ElMessage.success('已彻底删除');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function restoreAsset(row: AssetItem) {
    try {
      await ElMessageBox.confirm(
        `确认撤销删除资产「${row.name}」？将恢复为正常资产。`,
        '撤销删除确认',
        {
          type: 'warning',
        },
      );
    } catch {
      return;
    }
    try {
      await restoreAssetApi(row.id);
      ElMessage.success('已恢复');
      await Promise.all([loadData(), loadHierarchyAssetCounts()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  function openBorrowDialog(row: AssetItem) {
    if (!canBorrowAvailableAsset(row, currentUserId.value)) {
      ElMessage.warning('当前保管人不能借用自己保管的资产');
      return;
    }
    currentAssetForAction.value = row;
    borrowDialogVisible.value = true;
  }

  function openTransferDialog(row: AssetItem) {
    currentAssetForAction.value = row;
    transferDialogVisible.value = true;
  }

  const debouncedRemove = useDebounceFn(remove, 300);
  const debouncedPurge = useDebounceFn(purge, 300);
  const debouncedRestore = useDebounceFn(restoreAsset, 300);

  function onRowCommand(command: string, row: AssetItem) {
    switch (command) {
      case 'borrow': {
        openBorrowDialog(row);
        break;
      }
      case 'delete': {
        runHandled(debouncedRemove(row));
        break;
      }
      case 'purge': {
        runHandled(debouncedPurge(row));
        break;
      }
      case 'restore': {
        runHandled(debouncedRestore(row));
        break;
      }
      case 'transfer': {
        openTransferDialog(row);
        break;
      }
      // no default
    }
  }

  return {
    openCreate,
    openEdit,
    openDetail,
    onSaved,
    onImported,
    remove,
    purge,
    restoreAsset,
    onRowCommand,
    openBorrowDialog,
    openTransferDialog,
    debouncedRemove,
    debouncedPurge,
    debouncedRestore,
  };
}
