import type { ComputedRef, Ref } from 'vue';
import { computed, ref } from 'vue';

import { listMyFlowsPageApi, listPendingFlowsPageApi } from '#/api/material';
import {
  getMineApprovalsPageApi,
  getPendingApprovalsPageApi,
  getPendingReturnsPageApi,
} from '#/api/workflow';

import { combineAvailableCounts } from './approval-counts';
import { mergeSettledValue } from './dashboard-load-state';

export interface WorkspaceApprovalTodoState {
  myPendingApprovals: Ref<null | number>;
  myPendingMaterialFlows: Ref<null | number>;
  pendingApprovals: Ref<null | number>;
  pendingMaterialFlows: Ref<null | number>;
  pendingReturns: Ref<null | number>;
}

export interface WorkspaceApprovalAccess {
  canConfirmReturns: boolean;
  canHandleApprovals: boolean;
  canHandleMaterialFlows: boolean;
  canViewApprovals: boolean;
  canViewMaterialFlows: boolean;
}

export function createWorkspaceApprovalTodos(access: {
  canHandleApprovals: ComputedRef<boolean>;
  canHandleMaterialFlows: ComputedRef<boolean>;
  canViewApprovals: ComputedRef<boolean>;
  canViewMaterialFlows: ComputedRef<boolean>;
}) {
  const pendingApprovals = ref<null | number>(null);
  const myPendingApprovals = ref<null | number>(null);
  const pendingMaterialFlows = ref<null | number>(null);
  const myPendingMaterialFlows = ref<null | number>(null);
  const pendingReturns = ref<null | number>(null);
  const pendingMineCount = computed(() =>
    combineAvailableCounts([
      {
        enabled: access.canViewApprovals.value,
        value: myPendingApprovals.value,
      },
      {
        enabled: access.canViewMaterialFlows.value,
        value: myPendingMaterialFlows.value,
      },
    ]),
  );
  const pendingApprovalCount = computed(() =>
    combineAvailableCounts([
      {
        enabled: access.canHandleApprovals.value,
        value: pendingApprovals.value,
      },
      {
        enabled: access.canHandleMaterialFlows.value,
        value: pendingMaterialFlows.value,
      },
    ]),
  );

  return {
    myPendingApprovals,
    myPendingMaterialFlows,
    pendingApprovalCount,
    pendingApprovals,
    pendingMaterialFlows,
    pendingMineCount,
    pendingReturns,
  };
}

export function createWorkspaceApprovalTodoRequests(
  access: WorkspaceApprovalAccess,
  state: WorkspaceApprovalTodoState,
): {
  materialMine: Promise<null | number>;
  materialPending: Promise<null | number>;
  mine: Promise<null | number>;
  pending: Promise<null | number>;
  returns: Promise<null | number>;
} {
  const pending: Promise<null | number> = access.canHandleApprovals
    ? getPendingApprovalsPageApi({ page: 1, pageSize: 1 }).then(
        (result) => result.total,
      )
    : Promise.resolve(state.pendingApprovals.value);
  const mine: Promise<null | number> = access.canViewApprovals
    ? getMineApprovalsPageApi({
        page: 1,
        pageSize: 1,
        status: 'pending',
      }).then((result) => result.total)
    : Promise.resolve(state.myPendingApprovals.value);
  const returns: Promise<null | number> = access.canConfirmReturns
    ? getPendingReturnsPageApi({ page: 1, pageSize: 1 }).then(
        (result) => result.total,
      )
    : Promise.resolve(state.pendingReturns.value);
  const materialPending: Promise<null | number> = access.canHandleMaterialFlows
    ? listPendingFlowsPageApi({ page: 1, pageSize: 1 }).then(
        (result) => result.total,
      )
    : Promise.resolve(state.pendingMaterialFlows.value);
  const materialMine: Promise<null | number> = access.canViewMaterialFlows
    ? listMyFlowsPageApi({
        page: 1,
        pageSize: 1,
        status: 'pending',
      }).then((result) => result.total)
    : Promise.resolve(state.myPendingMaterialFlows.value);

  return { materialMine, materialPending, mine, pending, returns };
}

export function applyWorkspaceApprovalTodoResults(
  access: WorkspaceApprovalAccess,
  state: WorkspaceApprovalTodoState,
  pending: PromiseSettledResult<null | number>,
  mine: PromiseSettledResult<null | number>,
  returns: PromiseSettledResult<null | number>,
  materialPending: PromiseSettledResult<null | number>,
  materialMine: PromiseSettledResult<null | number>,
  nextErrors: {
    materialMine?: null | string;
    materialPending?: null | string;
    mine?: null | string;
    pending?: null | string;
    returns?: null | string;
  },
) {
  if (access.canHandleApprovals) {
    const nextPending = mergeSettledValue(
      state.pendingApprovals.value,
      pending,
    );
    state.pendingApprovals.value = nextPending.value;
    nextErrors.pending = nextPending.error;
  }
  if (access.canViewApprovals) {
    const nextMine = mergeSettledValue(state.myPendingApprovals.value, mine);
    state.myPendingApprovals.value = nextMine.value;
    nextErrors.mine = nextMine.error;
  }
  if (access.canConfirmReturns) {
    const nextReturns = mergeSettledValue(state.pendingReturns.value, returns);
    state.pendingReturns.value = nextReturns.value;
    nextErrors.returns = nextReturns.error;
  }
  if (access.canHandleMaterialFlows) {
    const nextMaterialPending = mergeSettledValue(
      state.pendingMaterialFlows.value,
      materialPending,
    );
    state.pendingMaterialFlows.value = nextMaterialPending.value;
    nextErrors.materialPending = nextMaterialPending.error;
  }
  if (access.canViewMaterialFlows) {
    const nextMaterialMine = mergeSettledValue(
      state.myPendingMaterialFlows.value,
      materialMine,
    );
    state.myPendingMaterialFlows.value = nextMaterialMine.value;
    nextErrors.materialMine = nextMaterialMine.error;
  }
}
