import type { Router } from 'vue-router';

import type { ComputedRef } from 'vue';
import { computed } from 'vue';

import { runHandled } from '#/utils/handled-promise';

import { myApplicationPath, pendingApprovalPath } from './approval-navigation';

export function createWorkspaceNavigation(state: {
  canHandleApprovals: ComputedRef<boolean>;
  canHandleMaterialFlows: ComputedRef<boolean>;
  canViewApprovals: ComputedRef<boolean>;
  canViewAssets: ComputedRef<boolean>;
  canViewCategories: ComputedRef<boolean>;
  canViewMaterialFlows: ComputedRef<boolean>;
  canViewReports: ComputedRef<boolean>;
  router: Router;
}) {
  const approvalNavigationAccess = computed(() => ({
    canHandleApprovals: state.canHandleApprovals.value,
    canHandleMaterialFlows: state.canHandleMaterialFlows.value,
    canViewApprovals: state.canViewApprovals.value,
    canViewMaterialFlows: state.canViewMaterialFlows.value,
  }));
  const pendingApprovalTarget = computed(() =>
    pendingApprovalPath(approvalNavigationAccess.value),
  );
  const myApplicationTarget = computed(() =>
    myApplicationPath(approvalNavigationAccess.value),
  );
  const shortcuts = computed(() =>
    [
      {
        label: '资产列表',
        path: '/asset/list',
        visible: state.canViewAssets.value,
      },
      {
        label: '资产分类',
        path: '/asset/categories',
        visible: state.canViewCategories.value,
      },
      {
        label: '待我审批',
        path: pendingApprovalTarget.value,
        visible: !!pendingApprovalTarget.value,
      },
      {
        label: '我的申请',
        path: myApplicationTarget.value,
        visible: !!myApplicationTarget.value,
      },
      {
        label: '资产汇总',
        path: '/report/summary',
        visible: state.canViewReports.value,
      },
    ].filter((item) => item.visible),
  );

  function go(path: null | string) {
    if (!path) return;
    runHandled(state.router.push(path));
  }

  function goCategoryAssets(categoryCode: string) {
    if (!categoryCode) return;
    runHandled(
      state.router.push({
        path: '/asset/list',
        query: { categoryCode },
      }),
    );
  }

  return {
    approvalNavigationAccess,
    go,
    goCategoryAssets,
    myApplicationTarget,
    pendingApprovalTarget,
    shortcuts,
  };
}
