import { computed, onActivated, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { flattenActiveDepartments } from '#/utils/department-options';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';

import { bindAssetHierarchy } from './asset-hierarchy';
import { bindAssetListQueries } from './asset-list-queries';
import { createAssetListState } from './asset-list-state';
import {
  buildAssetRowActionAccess,
  canShowAllAssetExport,
} from './asset-row-actions';
import { bindAssetRowCommands } from './asset-row-commands';

export function useAssetListPage() {
  const { hasAccessByCodes } = useAccess();
  const route = useRoute();
  const userStore = useUserStore();
  const currentUserId = computed(() => Number(userStore.userInfo?.userId || 0));
  const state = createAssetListState();
  let dictionariesReady = false;
  const hierarchy = bindAssetHierarchy({
    categories: state.categories,
    categoryPage: state.categoryPage,
    categoryPageSize: state.categoryPageSize,
    categoryPath: state.categoryPath,
    flatMode: state.flatMode,
    hierarchyKeyword: state.hierarchyKeyword,
  });
  const queries = bindAssetListQueries({
    hasAccessByCodes,
    hierarchy,
    route,
    state,
  });
  const categoryOptions = computed(() =>
    queries.flattenCategories(state.categories.value),
  );
  const activeDepartmentOptions = computed(() =>
    flattenActiveDepartments(state.departments.value),
  );
  const canPurgeAsset = computed(() => hasAccessByCodes(['asset:purge']));
  const canRestoreAsset = computed(() => hasAccessByCodes(['asset:restore']));
  const assetRowActionAccess = computed(() =>
    buildAssetRowActionAccess(hasAccessByCodes),
  );
  const showAllAssetExport = computed(() =>
    canShowAllAssetExport(
      hierarchy.currentCategoryLevel.value,
      state.flatMode.value,
      assetRowActionAccess.value.canExport,
    ),
  );
  const commands = bindAssetRowCommands({
    ...state,
    currentUserId,
    loadData: queries.loadData,
    loadHierarchyAssetCounts: queries.loadHierarchyAssetCounts,
  });
  const {
    categoryPageSize,
    detail,
    detailLoading,
    detailRequestGuard,
    detailVisible,
    pageSizeOptions,
    query,
  } = state;

  onMounted(async () => {
    query.pageSize = await getDefaultPageSize();
    categoryPageSize.value = query.pageSize;
    pageSizeOptions.value = createPageSizeOptions(query.pageSize);
    await queries.loadDictionaries();
    dictionariesReady = true;
    const routed = await queries.applyCategoryCodeFromRoute();
    await Promise.all([
      routed ? Promise.resolve() : queries.loadData(),
      queries.loadHierarchyAssetCounts(),
    ]);
  });

  onActivated(() => {
    if (dictionariesReady && !route.query.categoryCode && !state.flatMode.value)
      queries.enterFlatMode();
  });

  watch(
    () => route.query.categoryCode,
    async (categoryCode, oldCategoryCode) => {
      if (categoryCode === oldCategoryCode || !dictionariesReady) return;
      const routed = await queries.applyCategoryCodeFromRoute();
      if (!routed) await queries.loadData();
    },
  );

  watch(detailVisible, (opened) => {
    if (!opened) {
      detailRequestGuard.invalidate();
      detailLoading.value = false;
      detail.value = null;
    }
  });

  return {
    activeDepartmentOptions,
    assetRowActionAccess,
    canPurgeAsset,
    canRestoreAsset,
    categoryOptions,
    currentUserId,
    route,
    showAllAssetExport,
    userStore,
    ...state,
    ...hierarchy,
    ...queries,
    ...commands,
  };
}
