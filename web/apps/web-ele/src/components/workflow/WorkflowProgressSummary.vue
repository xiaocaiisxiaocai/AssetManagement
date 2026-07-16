<script lang="ts" setup>
import type { WorkflowProgressStep } from '#/api/workflow';

import { computed } from 'vue';

import { getTerminalProgress } from './workflow-progress-summary';

const props = defineProps<{
  currentSteps?: WorkflowProgressStep[];
  nextSteps?: WorkflowProgressStep[];
  status: string;
}>();

const terminalProgress = computed(() => getTerminalProgress(props.status));

function people(step: WorkflowProgressStep) {
  if (step.assignees.length === 0) return '待系统确定处理人';
  return step.assignees
    .map(({ employeeNo, name }) => `${name}（${employeeNo}）`)
    .join('、');
}

function assigneeNames(
  step: WorkflowProgressStep,
  status: 'completed' | 'pending',
) {
  return step.assignees
    .filter((item) => item.status === status)
    .map(({ employeeNo, name }) => `${name}（${employeeNo}）`)
    .join('、');
}

const currentText = computed(() => {
  if (props.currentSteps?.length) {
    return props.currentSteps
      .map((step) => {
        const pending = assigneeNames(step, 'pending');
        return `${step.nodeName} · ${pending || '等待节点流转'}`;
      })
      .join('；');
  }
  return '等待进入下一审批节点';
});

const completedText = computed(() =>
  (props.currentSteps || [])
    .map((step) => {
      const completed = assigneeNames(step, 'completed');
      return completed ? `${step.nodeName} · ${completed}` : '';
    })
    .filter(Boolean)
    .join('；'),
);

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
    <div
      v-if="terminalProgress"
      class="workflow-summary-terminal"
      :class="`workflow-summary-terminal-${terminalProgress.tone}`"
    >
      {{ terminalProgress.text }}
    </div>
    <template v-else>
      <div class="workflow-summary-line">
        <span class="workflow-summary-label workflow-summary-current"
          >待处理</span
        >
        <span>{{ currentText }}</span>
      </div>
      <div
        v-if="completedText"
        class="workflow-summary-line workflow-summary-signed"
      >
        <span class="workflow-summary-label">已同意</span>
        <span>{{ completedText }}</span>
      </div>
      <div v-if="nextText" class="workflow-summary-line workflow-summary-next">
        <span class="workflow-summary-label">下一步</span>
        <span>{{ nextText }}</span>
      </div>
    </template>
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

.workflow-summary-signed {
  margin-top: 5px;
  color: var(--el-color-success);
  font-size: 12px;
}

.workflow-summary-current {
  color: var(--el-color-primary);
}

.workflow-summary-label {
  flex: none;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.workflow-summary-terminal {
  font-weight: 500;
}

.workflow-summary-terminal-success {
  color: var(--el-color-success);
}

.workflow-summary-terminal-danger {
  color: var(--el-color-danger);
}

.workflow-summary-terminal-info {
  color: var(--el-text-color-secondary);
}
</style>
