<script lang="ts" setup>
import type {
  ActionableWorkflowFlow,
  ActionableWorkflowNode,
} from '#/utils/workflow-action-nodes';

import { onBeforeUnmount, ref, shallowRef } from 'vue';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElMessage,
  ElOption,
  ElSelect,
} from 'element-plus';

import {
  findActionableWorkflowNode,
  getActionableWorkflowNodes,
  getDirectActionableWorkflowNode,
} from '#/utils/workflow-action-nodes';

defineOptions({ name: 'WorkflowNodeSelectDialog' });

const visible = ref(false);
const actionLabel = ref('处理');
const nodes = ref<ActionableWorkflowNode[]>([]);
const selectedNodeId = ref('');
const currentFlow = shallowRef<ActionableWorkflowFlow>();
let resolveSelection:
  | ((node: ActionableWorkflowNode | undefined) => void)
  | undefined;

function settle(node?: ActionableWorkflowNode) {
  const resolve = resolveSelection;
  resolveSelection = undefined;
  resolve?.(node);
}

function selectNode(
  flow: ActionableWorkflowFlow,
  nextActionLabel = '处理',
): Promise<ActionableWorkflowNode | undefined> {
  const availableNodes = getActionableWorkflowNodes(flow);
  if (availableNodes.length === 0) {
    ElMessage.warning('当前没有可操作的审批节点，请刷新待办列表');
    return Promise.resolve(undefined);
  }

  const directNode = getDirectActionableWorkflowNode(flow);
  if (directNode) return Promise.resolve(directNode);

  settle();
  currentFlow.value = flow;
  nodes.value = availableNodes;
  selectedNodeId.value = '';
  actionLabel.value = nextActionLabel;
  visible.value = true;

  return new Promise((resolve) => {
    resolveSelection = resolve;
  });
}

function confirmSelection() {
  const selected = currentFlow.value
    ? findActionableWorkflowNode(currentFlow.value, selectedNodeId.value)
    : undefined;
  if (!selected) {
    ElMessage.warning('请选择要处理的并行节点');
    return;
  }

  visible.value = false;
  settle(selected);
}

function cancelSelection() {
  visible.value = false;
  settle();
}

function handleClosed() {
  settle();
}

onBeforeUnmount(() => settle());

defineExpose({ selectNode });
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="false"
    title="选择处理节点"
    width="min(480px, calc(100vw - 32px))"
    @closed="handleClosed"
  >
    <ElForm label-position="top">
      <ElFormItem :label="`请选择本次${actionLabel}的并行节点`" required>
        <ElSelect
          v-model="selectedNodeId"
          placeholder="请选择处理节点"
          style="width: 100%"
        >
          <ElOption
            v-for="node in nodes"
            :key="node.id"
            :label="`${node.name}（${node.id}）`"
            :value="node.id"
          />
        </ElSelect>
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton @click="cancelSelection">取消</ElButton>
      <ElButton
        :disabled="!selectedNodeId"
        type="primary"
        @click="confirmSelection"
      >
        确认
      </ElButton>
    </template>
  </ElDialog>
</template>
