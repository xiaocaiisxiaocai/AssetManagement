import { computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { runHandled } from '#/utils/handled-promise';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';

import { notificationSource } from '../notification-flow-attempt';
import { createPendingActionState } from './pending-action-state';
import { bindPendingDecisionActions } from './pending-decision-actions';
import {
  bindPendingFilterActions,
  createPendingFilterState,
} from './pending-filter-state';
import { bindPendingListActions } from './pending-list-actions';
import {
  bindPendingViewActions,
  createPendingViewState,
} from './pending-view-state';

export function usePendingApprovals() {
  const { hasAccessByCodes } = useAccess();
  const route = useRoute();
  const router = useRouter();
  const userStore = useUserStore();
  const currentUserId = computed(() => Number(userStore.userInfo?.userId || 0));
  const canAddSign = computed(() => hasAccessByCodes(['approval:add-sign']));
  const canHandleMaterialFlow = computed(() =>
    hasAccessByCodes(['material-flow:approve']),
  );
  const filterState = createPendingFilterState();
  const viewState = createPendingViewState(
    filterState.assetPage,
    filterState.materialPage,
  );
  const actionState = createPendingActionState(currentUserId);
  const listActions = bindPendingListActions({
    activeSource: viewState.activeSource,
    addSignUser: actionState.addSignUser,
    assetFlows: viewState.assetFlows,
    assetTotal: viewState.assetTotal,
    canAddSign,
    canHandleMaterialFlow,
    currentPage: viewState.currentPage,
    detailVisible: actionState.detailVisible,
    flows: viewState.flows,
    handledDetailVisible: actionState.handledDetailVisible,
    handledSelected: actionState.handledSelected,
    listRequestGuard: actionState.listRequestGuard,
    loading: actionState.loading,
    materialFlows: viewState.materialFlows,
    materialTotal: viewState.materialTotal,
    openedNotificationFlowKey: actionState.openedNotificationFlowKey,
    opinion: actionState.opinion,
    query: filterState.query,
    route,
    router,
    selected: actionState.selected,
    selectedNodeId: actionState.selectedNodeId,
    users: actionState.users,
    viewMode: viewState.viewMode,
  });

  watch(
    () => [route.query.flowId, route.query.source],
    () => {
      viewState.activeSource.value = notificationSource(
        route.query.source,
        route.path,
      );
      viewState.currentPage.value.page = 1;
      runHandled(listActions.loadData());
    },
  );

  const decisionActions = bindPendingDecisionActions({
    actionLoading: actionState.actionLoading,
    addSignLoading: actionState.addSignLoading,
    addSignUser: actionState.addSignUser,
    addSignVisible: actionState.addSignVisible,
    canAddSign,
    cancelAddSignLoadingKey: actionState.cancelAddSignLoadingKey,
    currentUserId,
    detailVisible: actionState.detailVisible,
    flows: viewState.flows,
    loadData: listActions.loadData,
    materialActionLoadingIds: actionState.materialActionLoadingIds,
    opinion: actionState.opinion,
    resolveNodeId: listActions.resolveNodeId,
    selected: actionState.selected,
    users: actionState.users,
    workflowNodeSelector: actionState.workflowNodeSelector,
  });
  const filterActions = bindPendingFilterActions({
    currentPage: viewState.currentPage,
    loadData: listActions.loadData,
    query: filterState.query,
  });
  const viewActions = bindPendingViewActions({
    assetPage: filterState.assetPage,
    currentPage: viewState.currentPage,
    loadData: listActions.loadData,
    materialPage: filterState.materialPage,
    query: filterState.query,
  });

  onMounted(async () => {
    const pageSize = await getDefaultPageSize();
    filterState.assetPage.pageSize = pageSize;
    filterState.materialPage.pageSize = pageSize;
    filterState.pageSizeOptions.value = createPageSizeOptions(pageSize);
    viewState.activeSource.value = notificationSource(
      route.query.source,
      route.path,
    );
    await listActions.loadData();
  });

  return {
    route,
    router,
    userStore,
    currentUserId,
    canAddSign,
    canHandleMaterialFlow,
    ...actionState,
    ...viewState,
    ...filterState,
    ...listActions,
    ...decisionActions,
    ...filterActions,
    ...viewActions,
  };
}
