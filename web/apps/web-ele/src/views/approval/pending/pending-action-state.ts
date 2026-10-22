import type { ApprovalWorkItem } from '../approval-work-items';

import type { UserOptionDto } from '#/api/user';
import type { ApprovalFlow } from '#/api/workflow';
import type WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';

import type { ComputedRef } from 'vue';
import { computed, ref } from 'vue';

import { BpmnTokenStatus } from '#/api/workflow';
import { createLatestRequestGuard } from '#/utils/latest-request';

import { excludeCurrentUserFromAddSignCandidates } from '../add-sign-access';

export function createPendingActionState(currentUserId: ComputedRef<number>) {
  const loading = ref(false);
  const actionLoading = ref(false);
  const addSignLoading = ref(false);
  const cancelAddSignLoadingKey = ref('');
  const detailVisible = ref(false);
  const addSignVisible = ref(false);
  const selected = ref<ApprovalFlow | null>(null);
  const handledSelected = ref<ApprovalWorkItem | null>(null);
  const handledDetailVisible = ref(false);
  const users = ref<UserOptionDto[]>([]);
  const materialActionLoadingIds = ref(new Set<number>());
  const releasingKeys = ref<string[]>([]);
  const opinion = ref('同意');
  const addSignUser = ref('');
  const selectedNodeId = ref('');
  const workflowNodeSelector = ref<InstanceType<
    typeof WorkflowNodeSelectDialog
  > | null>(null);
  const openedNotificationFlowKey = ref('');
  const listRequestGuard = createLatestRequestGuard();
  const addSignCandidates = computed(() =>
    excludeCurrentUserFromAddSignCandidates(users.value, currentUserId.value),
  );

  // 获取当前活跃节点信息
  const currentNodeInfo = computed(() => {
    if (!selected.value) return null;
    const nodeIds = selected.value.actionableNodeIds;
    if (nodeIds.length === 0) return null;

    const tokens = selected.value.bpmnTokens;
    const activeTokens = nodeIds
      .map((id) => tokens[id])
      .filter(
        (t): t is NonNullable<typeof t> =>
          t !== undefined && t.status === BpmnTokenStatus.Active,
      );

    return {
      count: activeTokens.length,
      names: activeTokens.map((t) => t.nodeName).join(', '),
      nodeIds,
    };
  });

  const activeNodeOptions = computed(() => {
    if (!selected.value) return [];

    return selected.value.actionableNodeIds
      .map((nodeId) => {
        const token = selected.value?.bpmnTokens[nodeId];
        return {
          nodeId,
          nodeName: token?.nodeName || nodeId,
        };
      })
      .filter(
        (item) =>
          selected.value?.bpmnTokens[item.nodeId]?.status ===
          BpmnTokenStatus.Active,
      );
  });

  const currentSignStates = computed(() => {
    if (!selected.value) return [];

    return selected.value.actionableNodeIds.flatMap((nodeId) => {
      const token = selected.value?.bpmnTokens[nodeId];
      if (!token?.signStates) return [];

      return Object.entries(token.signStates).map(([name, signed]) => ({
        displayName:
          users.value.find((user) => String(user.id) === name)?.name || name,
        name,
        nodeId,
        nodeName: token.nodeName,
        signed,
        canCancel:
          !signed && token.addedSigners?.[name] === currentUserId.value,
      }));
    });
  });

  return {
    actionLoading,
    activeNodeOptions,
    addSignCandidates,
    addSignLoading,
    addSignUser,
    addSignVisible,
    cancelAddSignLoadingKey,
    currentNodeInfo,
    currentSignStates,
    detailVisible,
    handledDetailVisible,
    handledSelected,
    listRequestGuard,
    loading,
    materialActionLoadingIds,
    openedNotificationFlowKey,
    opinion,
    releasingKeys,
    selected,
    selectedNodeId,
    users,
    workflowNodeSelector,
  };
}
