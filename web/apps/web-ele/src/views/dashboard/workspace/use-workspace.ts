import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import {
  applyWorkspaceApprovalTodoResults,
  createWorkspaceApprovalTodoRequests,
  createWorkspaceApprovalTodos,
} from './workspace-approval-todos';
import {
  applyWorkspaceAssetOverviewResults,
  createWorkspaceAssetOverview,
  createWorkspaceAssetOverviewRequests,
} from './workspace-asset-overview';
import { createWorkspaceNavigation } from './workspace-navigation';

export function useWorkspace() {
  const router = useRouter();
  const { hasAccessByCodes } = useAccess();
  const canViewAssets = computed(() => hasAccessByCodes(['asset:view']));
  const canViewCategories = computed(() => hasAccessByCodes(['category:view']));
  const canViewApprovals = computed(() => hasAccessByCodes(['approval:view']));
  const canHandleApprovals = computed(() =>
    hasAccessByCodes(['approval:handle']),
  );
  const canViewMaterialFlows = computed(() =>
    hasAccessByCodes(['material-flow:view']),
  );
  const canHandleMaterialFlows = computed(() =>
    hasAccessByCodes(['material-flow:approve']),
  );
  const canViewAnyApplications = computed(
    () => canViewApprovals.value || canViewMaterialFlows.value,
  );
  const canHandleAnyApprovals = computed(
    () => canHandleApprovals.value || canHandleMaterialFlows.value,
  );
  const canConfirmReturns = computed(() =>
    hasAccessByCodes(['approval:confirm-return']),
  );
  const canViewReports = computed(() => hasAccessByCodes(['report:view']));
  const loading = ref(false);
  const hasLoadedOnce = ref(false);

  type DashboardLoadKey =
    | 'assetSummary'
    | 'materialMine'
    | 'materialPending'
    | 'mine'
    | 'overdue'
    | 'pending'
    | 'returns';

  const loadErrors = ref<Partial<Record<DashboardLoadKey, null | string>>>({});
  const loadErrorLabels: Record<DashboardLoadKey, string> = {
    assetSummary: '资产概况',
    materialMine: '我的料件申请',
    materialPending: '待审料件流转',
    mine: '我的资产申请',
    overdue: '逾期资产',
    pending: '待审资产申请',
    returns: '待确认归还',
  };
  const failedLoadLabels = computed(() =>
    (Object.keys(loadErrorLabels) as DashboardLoadKey[])
      .filter((key) => Boolean(loadErrors.value[key]))
      .map((key) => loadErrorLabels[key]),
  );
  const initialLoading = computed(() => loading.value && !hasLoadedOnce.value);
  const assetOverview = createWorkspaceAssetOverview({
    canViewAssets,
    canViewReports,
  });
  const approvalTodos = createWorkspaceApprovalTodos({
    canHandleApprovals,
    canHandleMaterialFlows,
    canViewApprovals,
    canViewMaterialFlows,
  });
  const navigation = createWorkspaceNavigation({
    canHandleApprovals,
    canHandleMaterialFlows,
    canViewApprovals,
    canViewAssets,
    canViewCategories,
    canViewMaterialFlows,
    canViewReports,
    router,
  });

  async function loadData() {
    if (loading.value) return;
    loading.value = true;
    try {
      const assetRequests = createWorkspaceAssetOverviewRequests(
        canViewReports.value,
        assetOverview,
      );
      const approvalRequests = createWorkspaceApprovalTodoRequests(
        {
          canConfirmReturns: canConfirmReturns.value,
          canHandleApprovals: canHandleApprovals.value,
          canHandleMaterialFlows: canHandleMaterialFlows.value,
          canViewApprovals: canViewApprovals.value,
          canViewMaterialFlows: canViewMaterialFlows.value,
        },
        approvalTodos,
      );
      const [
        assetSummary,
        overdue,
        pending,
        mine,
        returns,
        materialPending,
        materialMine,
      ] = await Promise.allSettled([
        assetRequests.summary,
        assetRequests.overdue,
        approvalRequests.pending,
        approvalRequests.mine,
        approvalRequests.returns,
        approvalRequests.materialPending,
        approvalRequests.materialMine,
      ]);
      const nextErrors = { ...loadErrors.value };
      applyWorkspaceAssetOverviewResults(
        canViewReports.value,
        assetOverview,
        assetSummary,
        overdue,
        nextErrors,
      );
      applyWorkspaceApprovalTodoResults(
        {
          canConfirmReturns: canConfirmReturns.value,
          canHandleApprovals: canHandleApprovals.value,
          canHandleMaterialFlows: canHandleMaterialFlows.value,
          canViewApprovals: canViewApprovals.value,
          canViewMaterialFlows: canViewMaterialFlows.value,
        },
        approvalTodos,
        pending,
        mine,
        returns,
        materialPending,
        materialMine,
        nextErrors,
      );
      loadErrors.value = nextErrors;
    } finally {
      hasLoadedOnce.value = true;
      loading.value = false;
    }
  }

  onMounted(loadData);

  return {
    router,
    canViewAssets,
    canViewCategories,
    canViewApprovals,
    canHandleApprovals,
    canViewMaterialFlows,
    canHandleMaterialFlows,
    canViewAnyApplications,
    canHandleAnyApprovals,
    approvalNavigationAccess: navigation.approvalNavigationAccess,
    pendingApprovalTarget: navigation.pendingApprovalTarget,
    myApplicationTarget: navigation.myApplicationTarget,
    canConfirmReturns,
    canViewReports,
    loading,
    hasLoadedOnce,
    summary: assetOverview.summary,
    overdueRows: assetOverview.overdueRows,
    overdueTotal: assetOverview.overdueTotal,
    seriousOverdueCount: assetOverview.seriousOverdueCount,
    pendingApprovals: approvalTodos.pendingApprovals,
    myPendingApprovals: approvalTodos.myPendingApprovals,
    pendingMaterialFlows: approvalTodos.pendingMaterialFlows,
    myPendingMaterialFlows: approvalTodos.myPendingMaterialFlows,
    pendingReturns: approvalTodos.pendingReturns,
    loadErrors,
    failedLoadLabels,
    initialLoading,
    formatCount: assetOverview.formatCount,
    pendingMineCount: approvalTodos.pendingMineCount,
    pendingApprovalCount: approvalTodos.pendingApprovalCount,
    categoryTopRows: assetOverview.categoryTopRows,
    metricCards: assetOverview.metricCards,
    shortcuts: navigation.shortcuts,
    loadData,
    go: navigation.go,
    goCategoryAssets: navigation.goCategoryAssets,
  };
}
