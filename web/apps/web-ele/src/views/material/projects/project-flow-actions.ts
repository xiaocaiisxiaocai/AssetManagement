import type { MaterialFlowItem } from '#/api/material';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  approveFlowApi,
  listHandledFlowsPageApi,
  listMyFlowsPageApi,
  listPendingFlowsPageApi,
  rejectFlowApi,
  releaseMaterialFlowApi,
  withdrawMaterialFlowApi,
} from '#/api/material';
import { runHandled } from '#/utils/handled-promise';
import { formatWorkflowNode } from '#/utils/workflow-action-nodes';

export function bindProjectFlowActions(
  page: Record<string, any>,
  deps: {
    loadProjectMaterials: (projectId?: number) => Promise<void>;
  },
) {
  const { loadProjectMaterials } = deps;
  const {
    currentProject,
    currentMaterialActionAccess,
    materialActionAccess,
    isCurrentProjectReadOnly,
    followupDrawerVisible,
    pendingFlowRequestGuard,
    handledFlowRequestGuard,
    myFlowRequestGuard,
    workflowNodeSelector,
    pendingFlowLoading,
    handledFlowLoading,
    myFlowLoading,
    pendingFlows,
    handledFlows,
    myFlows,
    pendingFlowTotal,
    handledFlowTotal,
    myFlowTotal,
    pendingFlowQuery,
    myFlowQuery,
    handledFlowQuery,
  } = page;
  const processingFlowIds = page.processingFlowIds as { value: number[] };

  function beginFlowProcessing(flowId: number) {
    if (processingFlowIds.value.includes(flowId)) return false;
    processingFlowIds.value = [...processingFlowIds.value, flowId];
    return true;
  }

  function endFlowProcessing(flowId: number) {
    processingFlowIds.value = processingFlowIds.value.filter(
      (id) => id !== flowId,
    );
  }

  type ProjectFlowListKind = 'handled' | 'mine' | 'pending';

  /** 待我审批、我已处理按账号审批权限展示；项目阶段只决定能否在工作台内处理。 */
  function canViewApprovalLists() {
    return materialActionAccess.value?.canApprove === true;
  }

  function canHandleApprovals() {
    return (
      !isCurrentProjectReadOnly.value &&
      currentMaterialActionAccess.value?.canApprove === true
    );
  }

  function flowListState(kind: ProjectFlowListKind) {
    if (kind === 'handled') {
      return {
        flows: handledFlows,
        loading: handledFlowLoading,
        loadPage: listHandledFlowsPageApi,
        query: handledFlowQuery,
        requestGuard: handledFlowRequestGuard,
        total: handledFlowTotal,
      };
    }
    if (kind === 'mine') {
      return {
        flows: myFlows,
        loading: myFlowLoading,
        loadPage: listMyFlowsPageApi,
        query: myFlowQuery,
        requestGuard: myFlowRequestGuard,
        total: myFlowTotal,
      };
    }
    return {
      flows: pendingFlows,
      loading: pendingFlowLoading,
      loadPage: listPendingFlowsPageApi,
      query: pendingFlowQuery,
      requestGuard: pendingFlowRequestGuard,
      total: pendingFlowTotal,
    };
  }

  async function loadProjectFlowList(
    projectId: number,
    kind: ProjectFlowListKind,
  ) {
    if (kind !== 'mine' && !canViewApprovalLists()) return;
    const { flows, loading, loadPage, query, requestGuard, total } =
      flowListState(kind);
    loading.value = true;
    const requestGeneration = requestGuard.next();
    try {
      const result = await loadPage({ ...query, projectId });
      if (
        !requestGuard.isLatest(requestGeneration) ||
        !followupDrawerVisible.value ||
        currentProject.value?.id !== projectId
      ) {
        return;
      }
      const lastPage = Math.max(1, Math.ceil(result.total / query.pageSize));
      if (query.page > lastPage) {
        query.page = lastPage;
        await loadProjectFlowList(projectId, kind);
        return;
      }
      flows.value = result.items;
      total.value = result.total;
    } finally {
      if (requestGuard.isLatest(requestGeneration)) {
        loading.value = false;
      }
    }
  }

  async function loadProjectFlows(
    projectId = currentProject.value?.id,
    kind?: ProjectFlowListKind,
  ) {
    if (!projectId) return;
    if (kind) {
      await loadProjectFlowList(projectId, kind);
      return;
    }
    const kinds: ProjectFlowListKind[] = canViewApprovalLists()
      ? ['pending', 'handled', 'mine']
      : ['mine'];
    await Promise.all(
      kinds.map((item) => loadProjectFlowList(projectId, item)),
    );
  }

  async function approveFlow(row: MaterialFlowItem) {
    if (!canHandleApprovals()) return;
    if (!beginFlowProcessing(row.id)) return;
    try {
      const node = await workflowNodeSelector.value?.selectNode(row, '通过');
      if (!node) return;
      await ElMessageBox.confirm(
        `确认通过料件「${row.materialName}」的流转申请？处理节点：${formatWorkflowNode(node)}`,
        '审批通过',
        { type: 'warning' },
      );
      await approveFlowApi(row.id, '同意', node.id);
      ElMessage.success('已通过');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      endFlowProcessing(row.id);
    }
  }

  async function rejectFlow(row: MaterialFlowItem) {
    if (!canHandleApprovals()) return;
    if (!beginFlowProcessing(row.id)) return;
    try {
      const node = await workflowNodeSelector.value?.selectNode(row, '驳回');
      if (!node) return;
      let reason = '不同意';
      const result = await ElMessageBox.prompt(
        `请输入驳回原因。处理节点：${formatWorkflowNode(node)}`,
        '驳回',
        {
          inputPlaceholder: '驳回原因',
          inputValidator: (value: string) =>
            (value ?? '').length <= 500 || '驳回原因不能超过 500 个字符',
        },
      );
      reason = result.value || reason;
      await rejectFlowApi(row.id, reason, node.id);
      ElMessage.success('已驳回');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      endFlowProcessing(row.id);
    }
  }

  async function releaseFlow(row: MaterialFlowItem) {
    if (row.status !== 'pending' || row.canReleaseStuck !== true) return;
    if (!beginFlowProcessing(row.id)) return;
    try {
      await ElMessageBox.confirm(
        `确认终止料件「${row.materialName}」的流转申请？流程定义已变更，当前节点无法继续。终止后料件保管人保持不变，请重新发起。`,
        '终止并重新发起',
        {
          cancelButtonText: '取消',
          confirmButtonText: '确认终止',
          type: 'warning',
        },
      );
      await releaseMaterialFlowApi(row.id);
      ElMessage.success('流转已终止，请重新发起');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      endFlowProcessing(row.id);
    }
  }

  async function withdrawFlow(row: MaterialFlowItem) {
    if (row.canReleaseStuck === true) {
      await releaseFlow(row);
      return;
    }
    if (isCurrentProjectReadOnly.value || row.canWithdraw !== true) return;
    if (!beginFlowProcessing(row.id)) return;
    try {
      await ElMessageBox.confirm(
        `确认撤回料件「${row.materialName}」的流转申请？`,
        '撤回确认',
        { type: 'warning' },
      );
      await withdrawMaterialFlowApi(row.id);
      ElMessage.success('已撤回');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      endFlowProcessing(row.id);
    }
  }

  function onPendingFlowPageSizeChange() {
    pendingFlowQuery.page = 1;
    runHandled(loadProjectFlows(undefined, 'pending'));
  }

  function onHandledFlowPageSizeChange() {
    handledFlowQuery.page = 1;
    runHandled(loadProjectFlows(undefined, 'handled'));
  }

  function onMyFlowPageSizeChange() {
    myFlowQuery.page = 1;
    runHandled(loadProjectFlows(undefined, 'mine'));
  }

  return {
    loadProjectFlows,
    approveFlow,
    rejectFlow,
    releaseFlow,
    withdrawFlow,
    onPendingFlowPageSizeChange,
    onHandledFlowPageSizeChange,
    onMyFlowPageSizeChange,
  };
}
