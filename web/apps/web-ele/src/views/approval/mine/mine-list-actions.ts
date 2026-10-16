import type { ApprovalWorkItem } from '../approval-work-items';

import { ElMessage, ElMessageBox } from 'element-plus';

import { listMyFlowsPageApi, withdrawMaterialFlowApi } from '#/api/material';
import { nonempty } from '#/api/repeat-params';
import { getMineApprovalsPageApi, withdrawApprovalApi } from '#/api/workflow';
import { runHandled } from '#/utils/handled-promise';

import {
  normalizeAssetApproval,
  normalizeMaterialFlow,
} from '../approval-work-items';
import {
  beginNotificationFlowAttempt,
  notificationSource,
  withoutNotificationFlowId,
} from '../notification-flow-attempt';

export function bindMineListActions(page: Record<string, any>) {
  const {
    route,
    router,
    canViewMaterialFlow,
    loading,
    withdrawingKeys,
    progressVisible,
    selectedFlow,
    openedNotificationFlowKey,
    listRequestGuard,
    activeSource,
    assetFlows,
    materialFlows,
    assetTotal,
    materialTotal,
    query,
    currentPage,
    flows,
  } = page;

  async function loadData() {
    const requestGeneration = listRequestGuard.next();
    loading.value = true;
    let requestedNotificationKey = '';
    try {
      const page = currentPage.value;
      const notificationFlowId = Number(route.query.flowId || 0);
      const targetSource = notificationSource(route.query.source, route.path);
      const attempt = beginNotificationFlowAttempt(
        notificationFlowId,
        targetSource,
        activeSource.value,
        openedNotificationFlowKey.value,
      );
      const notificationKey = attempt.key;
      const flowId = attempt.requestedFlowId;
      if (flowId) {
        requestedNotificationKey = notificationKey;
        openedNotificationFlowKey.value = attempt.consumedKey;
      }
      if (activeSource.value === 'asset') {
        const result = await getMineApprovalsPageApi({
          bizTypes: flowId ? undefined : nonempty(query.bizTypes),
          flowId: route.query.source === 'material' ? undefined : flowId,
          keyword: flowId ? undefined : query.keyword.trim() || undefined,
          page: flowId ? 1 : page.page,
          pageSize: page.pageSize,
          statuses: flowId ? undefined : nonempty(query.statuses),
        });
        if (!listRequestGuard.isLatest(requestGeneration)) return;
        const lastPage = Math.max(1, Math.ceil(result.total / page.pageSize));
        if (page.page > lastPage) {
          page.page = lastPage;
          await loadData();
          return;
        }
        assetFlows.value = result.items.map((flow) =>
          normalizeAssetApproval(flow),
        );
        assetTotal.value = result.total;
      } else if (canViewMaterialFlow.value) {
        const result = await listMyFlowsPageApi({
          flowId,
          keyword: flowId ? undefined : query.keyword.trim() || undefined,
          page: flowId ? 1 : page.page,
          pageSize: page.pageSize,
          statuses: flowId ? undefined : nonempty(query.statuses),
        });
        if (!listRequestGuard.isLatest(requestGeneration)) return;
        const lastPage = Math.max(1, Math.ceil(result.total / page.pageSize));
        if (page.page > lastPage) {
          page.page = lastPage;
          await loadData();
          return;
        }
        materialFlows.value = result.items.map((flow) =>
          normalizeMaterialFlow(flow),
        );
        materialTotal.value = result.total;
      }
      if (flowId) {
        if (!focusNotificationFlow()) {
          ElMessage.warning('该通知对应的申请已过期或当前无权查看');
        }
        clearNotificationFlowQuery();
      }
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
      if (requestedNotificationKey) clearNotificationFlowQuery();
    } finally {
      if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
    }
  }

  function clearNotificationFlowQuery() {
    if (!route.query.flowId) return;
    const nextQuery = withoutNotificationFlowId(route.query);
    runHandled(router.replace({ path: route.path, query: nextQuery }));
  }

  async function withdraw(row: ApprovalWorkItem) {
    try {
      await ElMessageBox.confirm(
        `确认撤回申请「${row.flowNo}」？撤回后当前审批立即终止。`,
        '撤回申请',
        {
          cancelButtonText: '取消',
          confirmButtonText: '确认撤回',
          type: 'warning',
        },
      );
    } catch {
      return;
    }

    withdrawingKeys.value.push(row.key);
    try {
      await (row.source === 'asset'
        ? withdrawApprovalApi(row.id)
        : withdrawMaterialFlowApi(row.id));
      ElMessage.success('申请已撤回');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      withdrawingKeys.value = withdrawingKeys.value.filter(
        (key: string) => key !== row.key,
      );
    }
  }

  function onPageSizeChange() {
    currentPage.value.page = 1;
    runHandled(loadData());
  }

  function search() {
    currentPage.value.page = 1;
    runHandled(loadData());
  }

  function resetQuery() {
    Object.assign(query, {
      bizTypes: [],
      keyword: '',
      statuses: [],
    });
    currentPage.value.page = 1;
    runHandled(loadData());
  }

  function onSourceChange() {
    query.bizTypes = [];
    currentPage.value.page = 1;
    runHandled(loadData());
  }

  function showProgress(row: ApprovalWorkItem) {
    selectedFlow.value = row;
    progressVisible.value = true;
  }

  function focusNotificationFlow() {
    const flowId = Number(route.query.flowId || 0);
    const source = notificationSource(route.query.source, route.path);
    if (!flowId) return false;
    const targetIndex = flows.value.findIndex(
      (item: ApprovalWorkItem) => item.id === flowId && item.source === source,
    );
    const target = flows.value[targetIndex];
    if (!target) return false;
    showProgress(target);
    return true;
  }

  return {
    loadData,
    clearNotificationFlowQuery,
    withdraw,
    onPageSizeChange,
    search,
    resetQuery,
    onSourceChange,
    showProgress,
    focusNotificationFlow,
  };
}
