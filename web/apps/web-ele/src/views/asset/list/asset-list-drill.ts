import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { AssetHierarchyModel } from './asset-hierarchy';
import type { LoadAssetList } from './asset-list-browse';
import type { AssetListState } from './asset-list-state';

import type { CategoryNode } from '#/api/base-data';

import { runHandled } from '#/utils/handled-promise';

export type AssetHierarchyDrillPage = {
  categories: AssetListState['categories'];
  categoryPage: AssetListState['categoryPage'];
  categoryPath: AssetListState['categoryPath'];
  findCategoryPathByCode: AssetHierarchyModel['findCategoryPathByCode'];
  flatMode: AssetListState['flatMode'];
  getHierarchyContext: AssetHierarchyModel['getHierarchyContext'];
  hierarchyKeyword: AssetListState['hierarchyKeyword'];
  MAX_CATEGORY_LEVEL: AssetHierarchyModel['MAX_CATEGORY_LEVEL'];
  query: AssetListState['query'];
  route: RouteLocationNormalizedLoaded;
  selectedCategoryId: AssetListState['selectedCategoryId'];
};

export function bindAssetHierarchyDrill(
  page: AssetHierarchyDrillPage,
  loadData: LoadAssetList,
) {
  const {
    categories,
    categoryPage,
    categoryPath,
    findCategoryPathByCode,
    flatMode,
    getHierarchyContext,
    hierarchyKeyword,
    MAX_CATEGORY_LEVEL,
    query,
    route,
    selectedCategoryId,
  } = page;

  async function applyCategoryCodeFromRoute() {
    const rawCode = route.query.categoryCode;
    const categoryCode = Array.isArray(rawCode) ? rawCode[0] : rawCode;
    const path = categoryCode
      ? findCategoryPathByCode(categories.value, categoryCode)
      : [];

    // 无分类参数或分类已失效时恢复全部明细，避免保留上一次分类结果。
    flatMode.value = path.length === 0;
    categoryPath.value = path;
    selectedCategoryId.value = path[path.length - 1] ?? null;
    query.categoryId = selectedCategoryId.value ?? undefined;
    query.page = 1;
    hierarchyKeyword.value = '';
    categoryPage.value = 1;
    if (path.length === 0) return false;

    await loadData();
    return true;
  }

  function drillIntoCategory(node: CategoryNode) {
    if (categoryPath.value.length >= MAX_CATEGORY_LEVEL) {
      return;
    }
    flatMode.value = false;
    const nextPath = [...categoryPath.value, node.id];
    categoryPath.value = nextPath;
    selectedCategoryId.value = node.id;
    query.categoryId =
      nextPath.length === MAX_CATEGORY_LEVEL ? node.id : undefined;
    query.page = 1;
    hierarchyKeyword.value = '';
    categoryPage.value = 1;
    if (nextPath.length === MAX_CATEGORY_LEVEL) {
      runHandled(loadData());
    }
  }

  function drillToCategoryPath(index: number) {
    flatMode.value = false;
    categoryPath.value =
      index < 0 ? [] : categoryPath.value.slice(0, index + 1);
    const parent = getHierarchyContext().parent;
    selectedCategoryId.value = parent?.id ?? null;
    query.categoryId =
      categoryPath.value.length === MAX_CATEGORY_LEVEL ? parent?.id : undefined;
    query.page = 1;
    hierarchyKeyword.value = '';
    categoryPage.value = 1;
    if (categoryPath.value.length === MAX_CATEGORY_LEVEL) {
      runHandled(loadData());
    }
  }

  function resetCategorySearch() {
    hierarchyKeyword.value = '';
    categoryPage.value = 1;
  }

  return {
    applyCategoryCodeFromRoute,
    drillIntoCategory,
    drillToCategoryPath,
    resetCategorySearch,
  };
}
