import type { ApprovalWorkItem } from '../approval-work-items';
import type {
  ApprovalListViewMode,
  ApprovalSource,
} from '../notification-flow-attempt';
import type { PendingListPage, PendingListQuery } from './pending-filter-state';

import type { ComputedRef } from 'vue';
import { computed, ref } from 'vue';

import { runHandled } from '#/utils/handled-promise';

export function createPendingViewState(
  assetPage: PendingListPage,
  materialPage: PendingListPage,
) {
  const activeSource = ref<ApprovalSource>('asset');
  const viewMode = ref<ApprovalListViewMode>('pending');
  const assetFlows = ref<ApprovalWorkItem[]>([]);
  const materialFlows = ref<ApprovalWorkItem[]>([]);
  const assetTotal = ref(0);
  const materialTotal = ref(0);
  const currentPage = computed(() =>
    activeSource.value === 'asset' ? assetPage : materialPage,
  );
  const currentPageModel = computed({
    get: () => currentPage.value,
    set: (value) => Object.assign(currentPage.value, value),
  });
  const flows = computed(() =>
    activeSource.value === 'asset' ? assetFlows.value : materialFlows.value,
  );
  const currentTotal = computed(() =>
    activeSource.value === 'asset' ? assetTotal.value : materialTotal.value,
  );

  return {
    activeSource,
    assetFlows,
    assetTotal,
    currentPage,
    currentPageModel,
    currentTotal,
    flows,
    materialFlows,
    materialTotal,
    viewMode,
  };
}

export function bindPendingViewActions(state: {
  assetPage: PendingListPage;
  currentPage: ComputedRef<PendingListPage>;
  loadData: () => Promise<void>;
  materialPage: PendingListPage;
  query: PendingListQuery;
}) {
  function onSourceChange() {
    state.query.bizType = '';
    state.currentPage.value.page = 1;
    runHandled(state.loadData());
  }

  function onViewModeChange() {
    state.assetPage.page = 1;
    state.materialPage.page = 1;
    runHandled(state.loadData());
  }

  return { onSourceChange, onViewModeChange };
}
