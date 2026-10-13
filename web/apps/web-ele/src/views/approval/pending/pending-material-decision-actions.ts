import type { ApprovalWorkItem } from '../approval-work-items';

import type { MaterialFlowItem } from '#/api/material';
import type WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';

import type { Ref } from 'vue';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  approveFlowApi as approveMaterialFlowApi,
  rejectFlowApi as rejectMaterialFlowApi,
} from '#/api/material';
import { formatWorkflowNode } from '#/utils/workflow-action-nodes';

export interface PendingMaterialDecisionContext {
  loadData: () => Promise<void>;
  materialActionLoadingIds: Ref<Set<number>>;
  workflowNodeSelector: Ref<InstanceType<
    typeof WorkflowNodeSelectDialog
  > | null>;
}

export function bindPendingMaterialDecisionActions(
  state: PendingMaterialDecisionContext,
) {
  const { loadData, materialActionLoadingIds, workflowNodeSelector } = state;

  async function approveMaterial(item: ApprovalWorkItem) {
    const flow = item.raw as MaterialFlowItem;
    const node = await workflowNodeSelector.value?.selectNode(flow, '通过');
    if (!node) return;
    try {
      await ElMessageBox.confirm(
        `确认通过料件「${item.objectName}」的流转申请？处理节点：${formatWorkflowNode(node)}`,
        '审批通过',
        { type: 'warning' },
      );
    } catch {
      return;
    }

    materialActionLoadingIds.value.add(item.id);
    try {
      await approveMaterialFlowApi(item.id, '同意', node.id);
      ElMessage.success('已通过');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      materialActionLoadingIds.value.delete(item.id);
    }
  }

  async function rejectMaterial(item: ApprovalWorkItem) {
    const flow = item.raw as MaterialFlowItem;
    const node = await workflowNodeSelector.value?.selectNode(flow, '驳回');
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

    materialActionLoadingIds.value.add(item.id);
    try {
      await rejectMaterialFlowApi(item.id, reason, node.id);
      ElMessage.success('已驳回');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      materialActionLoadingIds.value.delete(item.id);
    }
  }

  return { approveMaterial, rejectMaterial };
}
