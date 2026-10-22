import type { RouteLocationNormalizedLoaded, Router } from 'vue-router';

import type { ApprovalWorkItem } from '../approval-work-items';
import type {
  ApprovalListViewMode,
  ApprovalSource,
} from '../notification-flow-attempt';
import type { PendingListPage, PendingListQuery } from './pending-filter-state';

import type { UserOptionDto } from '#/api/user';
import type { ApprovalFlow } from '#/api/workflow';

import type { ComputedRef, Ref } from 'vue';

import { ElMessage, ElMessageBox } from 'element-plus';

import { nonempty } from '#/api/repeat-params';
import {
  listHandledFlowsPageApi,
  listPendingFlowsPageApi,
  releaseMaterialFlowApi,
} from '#/api/material';
import { getApproverOptionsApi } from '#/api/user';
import {
  getHandledApprovalsPageApi,
  getPendingApprovalsPageApi,
  releaseApprovalApi,
} from '#/api/workflow';
import { runHandled } from '#/utils/handled-promise';

import {
  normalizeAssetApproval,
  normalizeMaterialFlow,
} from '../approval-work-items';
import {
  approvalListRequestFlowId,
  beginNotificationFlowAttempt,
  notificationSource,
  withoutNotificationFlowId,
} from '../notification-flow-attempt';

export interface PendingListActionContext {
  activeSource: Ref<ApprovalSource>;
  addSignUser: Ref<string>;
  assetFlows: Ref<ApprovalWorkItem[]>;
  assetTotal: Ref<number>;
  canAddSign: ComputedRef<boolean>;
  canHandleMaterialFlow: ComputedRef<boolean>;
  currentPage: ComputedRef<PendingListPage>;
  detailVisible: Ref<boolean>;
  flows: ComputedRef<ApprovalWorkItem[]>;
  handledDetailVisible: Ref<boolean>;
  handledSelected: Ref<ApprovalWorkItem | null>;
  listRequestGuard: {
    isLatest: (requestGeneration: number) => boolean;
    next: () => number;
  };
  loading: Ref<boolean>;
  materialFlows: Ref<ApprovalWorkItem[]>;
  materialTotal: Ref<number>;
  openedNotificationFlowKey: Ref<string>;
  opinion: Ref<string>;
  query: PendingListQuery;
  releasingKeys: Ref<string[]>;
  route: RouteLocationNormalizedLoaded;
  router: Router;
  selected: Ref<ApprovalFlow | null>;
  selectedNodeId: Ref<string>;
  users: Ref<UserOptionDto[]>;
  viewMode: Ref<ApprovalListViewMode>;
}

export function bindPendingListActions(state: PendingListActionContext) {
  const {
    activeSource,
    addSignUser,
    assetFlows,
    assetTotal,
    canAddSign,
    canHandleMaterialFlow,
    currentPage,
    detailVisible,
    flows,
    handledDetailVisible,
    handledSelected,
    listRequestGuard,
    loading,
    materialFlows,
    materialTotal,
    openedNotificationFlowKey,
    opinion,
    query,
    releasingKeys,
    route,
    router,
    selected,
    selectedNodeId,
    users,
    viewMode,
  } = state;

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
      const requestedFlowId = approvalListRequestFlowId(
        viewMode.value,
        attempt.requestedFlowId,
      );
      if (requestedFlowId) {
        requestedNotificationKey = notificationKey;
        // 深链只尝试一次；即使目标已过期或无权限，也不能让 flowId 永久约束列表。
        openedNotificationFlowKey.value = attempt.consumedKey;
      }
      if (activeSource.value === 'asset') {
        const request =
          viewMode.value === 'pending'
            ? getPendingApprovalsPageApi
            : getHandledApprovalsPageApi;
        const result = await request({
          bizTypes: requestedFlowId ? undefined : nonempty(query.bizTypes),
          flowId: requestedFlowId,
          keyword: requestedFlowId
            ? undefined
            : query.keyword.trim() || undefined,
          page: requestedFlowId ? 1 : page.page,
          pageSize: page.pageSize,
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
      } else if (canHandleMaterialFlow.value) {
        const request =
          viewMode.value === 'pending'
            ? listPendingFlowsPageApi
            : listHandledFlowsPageApi;
        const result = await request({
          flowId: requestedFlowId,
          keyword: requestedFlowId
            ? undefined
            : query.keyword.trim() || undefined,
          page: requestedFlowId ? 1 : page.page,
          pageSize: page.pageSize,
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
      if (canAddSign.value && users.value.length === 0) {
        users.value = await getApproverOptionsApi();
        if (!listRequestGuard.isLatest(requestGeneration)) return;
      }
      if (requestedFlowId) {
        const target = flows.value.find(
          (item: ApprovalWorkItem) =>
            item.source === targetSource && item.id === notificationFlowId,
        );
        if (target) {
          if (target.source === 'asset') {
            openDetail(target);
          }
        } else {
          ElMessage.warning('该通知对应的待办已处理、已过期或当前无权查看');
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

  function openDetail(item: ApprovalWorkItem) {
    if (item.source !== 'asset') return;
    const flow = item.raw as ApprovalFlow;
    selected.value = flow;
    opinion.value = '同意';
    addSignUser.value = '';
    selectedNodeId.value = flow.actionableNodeIds[0] || '';
    detailVisible.value = true;
  }

  function openHandledDetail(item: ApprovalWorkItem) {
    handledSelected.value = item;
    handledDetailVisible.value = true;
  }

  async function releaseStuck(item: ApprovalWorkItem) {
    if (!item.canReleaseStuck || item.status !== 'pending') return;
    try {
      await ElMessageBox.confirm(
        `确认终止申请「${item.flowNo}」？流程定义已变更，当前节点无法继续。终止后业务状态保持不变，请重新发起。`,
        '终止并重新发起',
        {
          cancelButtonText: '取消',
          confirmButtonText: '确认终止',
          type: 'warning',
        },
      );
    } catch {
      return;
    }

    releasingKeys.value = [...releasingKeys.value, item.key];
    try {
      await (item.source === 'asset'
        ? releaseApprovalApi(item.id)
        : releaseMaterialFlowApi(item.id));
      ElMessage.success('流程已终止，请重新发起');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      releasingKeys.value = releasingKeys.value.filter(
        (key) => key !== item.key,
      );
    }
  }

  function resolveNodeId() {
    if (!selected.value) return undefined;
    if (selected.value.actionableNodeIds.length <= 1) {
      return selected.value.actionableNodeIds[0];
    }

    return selected.value.actionableNodeIds.includes(selectedNodeId.value)
      ? selectedNodeId.value
      : undefined;
  }

  return {
    clearNotificationFlowQuery,
    loadData,
    openDetail,
    openHandledDetail,
    releaseStuck,
    resolveNodeId,
  };
}
