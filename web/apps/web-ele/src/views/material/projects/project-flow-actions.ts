import type { MaterialFlowItem } from '#/api/material';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  approveFlowApi,
  listHandledFlowsPageApi,
  listMyFlowsPageApi,
  listPendingFlowsPageApi,
  rejectFlowApi,
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
    materialActionAccess,
    isCurrentProjectReadOnly,
    followupDrawerVisible,
    pendingFlowRequestGuard,
    handledFlowRequestGuard,
    myFlowRequestGuard,
    workflowNodeSelector,
    flowActiveTab,
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

  async function loadProjectFlows(projectId = currentProject.value?.id) {
    if (!projectId) return;
    const loadMine = flowActiveTab.value === 'mine';
    const loadHandled = flowActiveTab.value === 'handled';
    if (!loadMine && !materialActionAccess.value.canApprove) return;
    if (loadMine) myFlowLoading.value = true;
    else if (loadHandled) handledFlowLoading.value = true;
    else pendingFlowLoading.value = true;
    let requestGuard = pendingFlowRequestGuard;
    if (loadMine) requestGuard = myFlowRequestGuard;
    else if (loadHandled) requestGuard = handledFlowRequestGuard;
    const requestGeneration = requestGuard.next();
    try {
      let query = pendingFlowQuery;
      let loadFlowsPage = listPendingFlowsPageApi;
      if (loadMine) {
        query = myFlowQuery;
        loadFlowsPage = listMyFlowsPageApi;
      } else if (loadHandled) {
        query = handledFlowQuery;
        loadFlowsPage = listHandledFlowsPageApi;
      }
      const result = await loadFlowsPage({ ...query, projectId });
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
        await loadProjectFlows(projectId);
        return;
      }
      if (loadMine) {
        myFlows.value = result.items;
        myFlowTotal.value = result.total;
      } else if (loadHandled) {
        handledFlows.value = result.items;
        handledFlowTotal.value = result.total;
      } else {
        pendingFlows.value = result.items;
        pendingFlowTotal.value = result.total;
      }
    } finally {
      if (requestGuard.isLatest(requestGeneration)) {
        if (loadMine) myFlowLoading.value = false;
        else if (loadHandled) handledFlowLoading.value = false;
        else pendingFlowLoading.value = false;
      }
    }
  }

  async function approveFlow(row: MaterialFlowItem) {
    if (isCurrentProjectReadOnly.value) return;
    const node = await workflowNodeSelector.value?.selectNode(row, '通过');
    if (!node) return;
    try {
      await ElMessageBox.confirm(
        `确认通过料件「${row.materialName}」的流转申请？处理节点：${formatWorkflowNode(node)}`,
        '审批通过',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await approveFlowApi(row.id, '同意', node.id);
      ElMessage.success('已通过');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function rejectFlow(row: MaterialFlowItem) {
    if (isCurrentProjectReadOnly.value) return;
    const node = await workflowNodeSelector.value?.selectNode(row, '驳回');
    if (!node) return;
    let reason = '不同意';
    try {
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
    } catch {
      return;
    }
    try {
      await rejectFlowApi(row.id, reason, node.id);
      ElMessage.success('已驳回');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function withdrawFlow(row: MaterialFlowItem) {
    if (isCurrentProjectReadOnly.value || row.canWithdraw !== true) return;
    try {
      await ElMessageBox.confirm(
        `确认撤回料件「${row.materialName}」的流转申请？`,
        '撤回确认',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await withdrawMaterialFlowApi(row.id);
      ElMessage.success('已撤回');
      await Promise.all([loadProjectFlows(), loadProjectMaterials()]);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  function onProjectTabChange(name: number | string) {
    if (name === 'materials') {
      runHandled(loadProjectMaterials());
    }
    if (name === 'flows') {
      runHandled(loadProjectFlows());
    }
  }

  function onFlowTabChange() {
    runHandled(loadProjectFlows());
  }

  function onPendingFlowPageSizeChange() {
    pendingFlowQuery.page = 1;
    runHandled(loadProjectFlows());
  }

  function onHandledFlowPageSizeChange() {
    handledFlowQuery.page = 1;
    runHandled(loadProjectFlows());
  }

  function onMyFlowPageSizeChange() {
    myFlowQuery.page = 1;
    runHandled(loadProjectFlows());
  }

  return {
    loadProjectFlows,
    approveFlow,
    rejectFlow,
    withdrawFlow,
    onProjectTabChange,
    onFlowTabChange,
    onPendingFlowPageSizeChange,
    onHandledFlowPageSizeChange,
    onMyFlowPageSizeChange,
  };
}
