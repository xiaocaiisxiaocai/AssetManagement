<script lang="ts" setup>
import type { WorkflowProgressStep } from '#/api/workflow';

import { formatDateTime } from '#/utils/date-format';

import { ElTag, ElTimeline, ElTimelineItem } from 'element-plus';

defineProps<{ steps: WorkflowProgressStep[] }>();

function assigneeText(step: WorkflowProgressStep) {
  return step.assignees.length
    ? step.assignees
        .map(({ employeeNo, name }) => `${name}（${employeeNo}）`)
        .join('、')
    : '待系统确定处理人';
}
</script>

<template>
  <ElTimeline v-if="steps.length" class="workflow-progress-detail">
    <ElTimelineItem
      v-for="step in steps"
      :key="`${step.state}-${step.nodeId}`"
      :timestamp="
        step.completedAt
          ? formatDateTime(step.completedAt)
          : step.startedAt
            ? formatDateTime(step.startedAt)
            : undefined
      "
      :type="
        step.state === 'completed'
          ? 'success'
          : step.state === 'current'
            ? 'primary'
            : 'info'
      "
    >
      <div class="workflow-step-title">
        <strong>{{ step.nodeName }}</strong>
        <ElTag v-if="step.state === 'completed'" size="small" type="success"
          >已完成</ElTag
        >
        <ElTag v-else-if="step.state === 'current'" size="small">当前处理</ElTag>
        <ElTag v-else size="small" type="info">
          {{ step.isPossible ? '可能下一步' : '下一步' }}
        </ElTag>
      </div>
      <div class="workflow-step-people">处理人：{{ assigneeText(step) }}</div>
      <div v-if="step.opinion" class="workflow-step-opinion">
        意见：{{ step.opinion }}
      </div>
    </ElTimelineItem>
  </ElTimeline>
  <div v-else class="workflow-progress-empty">暂无流转记录</div>
</template>

<style scoped>
.workflow-progress-detail {
  padding: 8px 4px 0;
}

.workflow-step-title {
  display: flex;
  gap: 8px;
  align-items: center;
}

.workflow-step-people,
.workflow-step-opinion {
  margin-top: 6px;
  color: var(--el-text-color-secondary);
}

.workflow-progress-empty {
  padding: 24px;
  color: var(--el-text-color-secondary);
  text-align: center;
}
</style>
