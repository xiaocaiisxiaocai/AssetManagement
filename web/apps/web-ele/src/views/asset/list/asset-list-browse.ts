import type { AssetHierarchyModel } from './asset-hierarchy';
import type { AssetListState } from './asset-list-state';

import type { AssetQuery } from '#/api/asset';

import {
  exportAssetsApi,
  getAssetCategoryCountsApi,
  getAssetListApi,
} from '#/api/asset';
import { runHandled } from '#/utils/handled-promise';

export type LoadAssetList = () => Promise<void>;

export type AssetListLoadingPage = {
  assets: AssetListState['assets'];
  categoryAssetCounts: AssetListState['categoryAssetCounts'];
  categoryPath: AssetListState['categoryPath'];
  exportingAllAssets: AssetListState['exportingAllAssets'];
  flatMode: AssetListState['flatMode'];
  hierarchyParent: AssetHierarchyModel['hierarchyParent'];
  isAssetStage: AssetHierarchyModel['isAssetStage'];
  listRequestGuard: AssetListState['listRequestGuard'];
  loading: AssetListState['loading'];
  query: AssetListState['query'];
  selectedCategoryId: AssetListState['selectedCategoryId'];
  total: AssetListState['total'];
};

export function bindAssetListLoading(page: AssetListLoadingPage) {
  const {
    assets,
    categoryAssetCounts,
    categoryPath,
    exportingAllAssets,
    flatMode,
    hierarchyParent,
    isAssetStage,
    listRequestGuard,
    loading,
    query,
    selectedCategoryId,
    total,
  } = page;

  function buildQuery(): AssetQuery {
    return {
      assetNo: query.assetNo || undefined,
      categoryId: query.categoryId,
      custodianId: query.custodianId,
      deleteStatus: query.deleteStatus,
      departmentId: query.departmentId,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
    };
  }

  async function loadData() {
    const requestGeneration = listRequestGuard.next();
    loading.value = true;
    try {
      const result = await getAssetListApi(buildQuery());
      if (!listRequestGuard.isLatest(requestGeneration)) return;
      assets.value = result.items;
      total.value = result.total;
    } finally {
      if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
    }
  }

  async function loadHierarchyAssetCounts() {
    categoryAssetCounts.value = await getAssetCategoryCountsApi();
  }

  function resetQuery() {
    Object.assign(query, {
      assetNo: '',
      categoryId: isAssetStage.value ? hierarchyParent.value?.id : undefined,
      custodianId: undefined,
      deleteStatus: 'all',
      departmentId: undefined,
      name: '',
      page: 1,
      status: undefined,
    });
    selectedCategoryId.value = isAssetStage.value
      ? (hierarchyParent.value?.id ?? null)
      : null;
    runHandled(loadData());
  }

  function search() {
    query.page = 1;
    runHandled(loadData());
  }

  function enterFlatMode() {
    flatMode.value = true;
    categoryPath.value = [];
    selectedCategoryId.value = null;
    Object.assign(query, {
      assetNo: '',
      categoryId: undefined,
      custodianId: undefined,
      deleteStatus: 'all',
      departmentId: undefined,
      name: '',
      page: 1,
      status: undefined,
    });
    runHandled(loadData());
  }

  function exitFlatMode() {
    flatMode.value = false;
    categoryPath.value = [];
    selectedCategoryId.value = null;
    query.categoryId = undefined;
  }

  async function exportAllAssets() {
    if (exportingAllAssets.value) return;
    exportingAllAssets.value = true;
    try {
      const response = await exportAssetsApi({ deleteStatus: 'all' });
      downloadBlob(response.data, '全部资产.xlsx');
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      exportingAllAssets.value = false;
    }
  }

  function downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }

  return {
    buildQuery,
    downloadBlob,
    enterFlatMode,
    exitFlatMode,
    exportAllAssets,
    loadData,
    loadHierarchyAssetCounts,
    resetQuery,
    search,
  };
}
