<script lang="ts" setup>
import type { WorkflowProgressStep } from '#/api/workflow';

import { computed } from 'vue';

const props = defineProps<{
  currentSteps?: WorkflowProgressStep[];
  nextSteps?: WorkflowProgressStep[];
  status: string;
}>();

function people(step: WorkflowProgressStep) {
  if (step.assignees.length === 0) return '待系统确定处理人';
  return step.assignees
    .map(({ employeeNo, name }) => `${name}（${employeeNo}）`)
    .join('、');
}

const currentText = computed(() => {
  if (props.currentSteps?.length) {
    return props.currentSteps
      .map((step) => `${step.nodeName} · ${people(step)}`)
      .join('；');
  }
  return props.status === 'pending' ? '等待进入下一审批节点' : '流程已结束';
});

const nextText = computed(() => {
  if (!props.nextSteps?.length) {
    return props.status === 'pending' ? '当前节点通过后流程结束' : '';
  }
  return props.nextSteps
    .map(
      (step) =>
        `${step.isPossible ? '可能进入：' : ''}${step.nodeName} · ${people(step)}`,
    )
    .join('；');
});
</script>

<template>
  <div class="workflow-summary">
    <div class="workflow-summary-line">
      <span class="workflow-summary-label">当前</span>
      <span>{{ currentText }}</span>
    </div>
    <div v-if="nextText" class="workflow-summary-line workflow-summary-next">
      <span class="workflow-summary-label">下一步</span>
      <span>{{ nextText }}</span>
    </div>
  </div>
</template>

<style scoped>
.workflow-summary {
  min-width: 240px;
  line-height: 1.45;
}

.workflow-summary-line {
  display: flex;
  gap: 8px;
}

.workflow-summary-next {
  margin-top: 5px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.workflow-summary-label {
  flex: none;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
