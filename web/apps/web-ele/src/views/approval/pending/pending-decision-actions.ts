import type { ApprovalWorkItem } from '../approval-work-items';
import type { PendingMaterialDecisionContext } from './pending-material-decision-actions';

import type { UserOptionDto } from '#/api/user';
import type { ApprovalActionPayload, ApprovalFlow } from '#/api/workflow';

import type { ComputedRef, Ref } from 'vue';
import { computed } from 'vue';

import { ElMessage, ElMessageBox } from 'element-plus';

import { getApproverOptionsApi } from '#/api/user';
import {
  addSignFlowApi,
  approveFlowApi,
  cancelAddSignFlowApi,
  rejectFlowApi,
} from '#/api/workflow';

import { canUserInitiateAddSign } from '../add-sign-access';
import {
  findApprovalWorkItemIndex,
  normalizeAssetApproval,
} from '../approval-work-items';
import { bindPendingMaterialDecisionActions } from './pending-material-decision-actions';

export interface PendingDecisionActionContext
  extends PendingMaterialDecisionContext {
  actionLoading: Ref<boolean>;
  addSignLoading: Ref<boolean>;
  addSignUser: Ref<string>;
  addSignVisible: Ref<boolean>;
  canAddSign: ComputedRef<boolean>;
  cancelAddSignLoadingKey: Ref<string>;
  currentUserId: ComputedRef<number>;
  detailVisible: Ref<boolean>;
  flows: ComputedRef<ApprovalWorkItem[]>;
  opinion: Ref<string>;
  resolveNodeId: () => string | undefined;
  selected: Ref<ApprovalFlow | null>;
  users: Ref<UserOptionDto[]>;
}

export function bindPendingDecisionActions(
  state: PendingDecisionActionContext,
) {
  const {
    actionLoading,
    addSignLoading,
    addSignUser,
    addSignVisible,
    canAddSign,
    cancelAddSignLoadingKey,
    currentUserId,
    detailVisible,
    flows,
    loadData,
    materialActionLoadingIds,
    opinion,
    resolveNodeId,
    selected,
    users,
    workflowNodeSelector,
  } = state;

  const canCurrentUserAddSign = computed(() => {
    if (!canAddSign.value || !selected.value) return false;
    const nodeId = resolveNodeId();
    if (!nodeId) return false;

    return canUserInitiateAddSign(
      selected.value.bpmnTokens[nodeId],
      currentUserId.value,
    );
  });

  async function cancelAddSign(item: {
    displayName: string;
    name: string;
    nodeId: string;
  }) {
    if (!selected.value) return;
    try {
      await ElMessageBox.confirm(
        `确认取消对“${item.displayName}”的加签吗？`,
        '取消加签',
        {
          type: 'warning',
          confirmButtonText: '确认取消',
          cancelButtonText: '返回',
        },
      );
    } catch {
      return;
    }
    cancelAddSignLoadingKey.value = `${item.nodeId}-${item.name}`;
    try {
      const updated = await cancelAddSignFlowApi(selected.value.id, {
        nodeId: item.nodeId,
        who: item.name,
      });
      selected.value = updated;
      const index = findApprovalWorkItemIndex(flows.value, 'asset', updated.id);
      if (index >= 0) flows.value[index] = normalizeAssetApproval(updated);
      ElMessage.success('已取消加签');
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      cancelAddSignLoadingKey.value = '';
    }
  }

  function ensureNodeSelected() {
    if (!selected.value || selected.value.actionableNodeIds.length === 0) {
      ElMessage.warning('当前没有可操作的审批节点，请刷新待办列表');
      return false;
    }
    if (selected.value.actionableNodeIds.length > 1 && !resolveNodeId()) {
      ElMessage.warning('请选择要处理的并行节点');
      return false;
    }

    return true;
  }

  async function openAddSign() {
    if (!selected.value) return;
    if (!canCurrentUserAddSign.value) {
      ElMessage.warning('被加签人不能再次发起加签');
      return;
    }
    if (!ensureNodeSelected()) return;
    // 候选人（部门主管）可能在加签弹窗打开前被停用/新增，每次打开都重新拉取，
    // 避免长期停留在该页面导致候选人列表过期。
    users.value = await getApproverOptionsApi();
    addSignUser.value = '';
    addSignVisible.value = true;
  }

  async function addSign() {
    if (!selected.value) return;
    if (!addSignUser.value) {
      ElMessage.warning('请选择加签人');
      return;
    }
    if (!ensureNodeSelected()) return;

    addSignLoading.value = true;
    try {
      const updated = await addSignFlowApi(selected.value.id, {
        nodeId: resolveNodeId(),
        who: addSignUser.value,
      });
      selected.value = updated;
      const index = findApprovalWorkItemIndex(flows.value, 'asset', updated.id);
      if (index >= 0) flows.value[index] = normalizeAssetApproval(updated);
      ElMessage.success('已加签');
      addSignVisible.value = false;
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      addSignLoading.value = false;
    }
  }

  async function approve() {
    if (!selected.value) return;
    if (opinion.value.length > 380) {
      ElMessage.warning('审批意见不能超过 380 个字符');
      return;
    }
    if (!ensureNodeSelected()) return;
    actionLoading.value = true;
    try {
      const nodeId = resolveNodeId();
      if (!nodeId) return;
      const payload: ApprovalActionPayload = { nodeId, opinion: opinion.value };
      await approveFlowApi(selected.value.id, payload);
      ElMessage.success('已通过');
      detailVisible.value = false;
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      actionLoading.value = false;
    }
  }

  async function reject() {
    if (!selected.value) {
      ElMessage.warning('请选择要驳回的记录');
      return;
    }
    if (!opinion.value.trim()) {
      ElMessage.warning('请填写驳回理由');
      return;
    }
    if (opinion.value.length > 500) {
      ElMessage.warning('驳回理由不能超过 500 个字符');
      return;
    }
    if (!ensureNodeSelected()) return;
    actionLoading.value = true;
    try {
      const nodeId = resolveNodeId();
      if (!nodeId) return;
      await rejectFlowApi(selected.value.id, {
        nodeId,
        reason: opinion.value,
      });
      ElMessage.success('已驳回');
      detailVisible.value = false;
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      actionLoading.value = false;
    }
  }

  const { approveMaterial, rejectMaterial } =
    bindPendingMaterialDecisionActions({
      loadData,
      materialActionLoadingIds,
      workflowNodeSelector,
    });

  return {
    addSign,
    approve,
    approveMaterial,
    canCurrentUserAddSign,
    cancelAddSign,
    ensureNodeSelected,
    openAddSign,
    reject,
    rejectMaterial,
  };
}
