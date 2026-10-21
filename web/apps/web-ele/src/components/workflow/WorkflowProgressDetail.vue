<script lang="ts" setup>
import type { WorkflowProgressStep } from '#/api/workflow';

import { ElTag, ElTimeline, ElTimelineItem } from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

withDefaults(
  defineProps<{
    emptyText?: string;
    steps: WorkflowProgressStep[];
  }>(),
  { emptyText: '暂无流转记录' },
);

function isRejected(step: WorkflowProgressStep) {
  return step.assignees.some((person) => person.status === 'rejected');
}

function personStatusMeta(
  status: WorkflowProgressStep['assignees'][number]['status'],
) {
  return {
    completed: { label: '已同意', type: 'success' as const },
    pending: { label: '待处理', type: 'warning' as const },
    rejected: { label: '已驳回', type: 'danger' as const },
    skipped: { label: '未处理', type: 'info' as const },
  }[status];
}

function opinionText(step: WorkflowProgressStep) {
  return (step.opinion || '').replace(/^\[驳回\]\s*/, '');
}
</script>

<template>
  <ElTimeline v-if="steps.length > 0" class="workflow-progress-detail">
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
        isRejected(step)
          ? 'danger'
          : step.state === 'completed'
            ? 'success'
            : step.state === 'current'
              ? 'primary'
              : 'info'
      "
    >
      <div class="workflow-step-title">
        <strong>{{ step.nodeName }}</strong>
        <ElTag v-if="isRejected(step)" size="small" type="danger">已驳回</ElTag>
        <ElTag
          v-else-if="step.state === 'completed'"
          size="small"
          type="success"
        >
          已完成
        </ElTag>
        <ElTag v-else-if="step.state === 'current'" size="small">
          当前处理
        </ElTag>
        <ElTag v-else size="small" type="info">
          {{ step.isPossible ? '可能下一步' : '下一步' }}
        </ElTag>
      </div>
      <div v-if="step.assignees.length > 0" class="workflow-step-people">
        <div
          v-for="person in step.assignees"
          :key="person.userId"
          class="workflow-step-person"
        >
          <span>{{ person.name }}（{{ person.employeeNo }}）</span>
          <ElTag
            :type="personStatusMeta(person.status).type"
            effect="plain"
            size="small"
          >
            {{ personStatusMeta(person.status).label }}
          </ElTag>
        </div>
      </div>
      <div
        v-else-if="step.state === 'completed' && step.completedBy"
        class="workflow-step-people"
      >
        处理人：{{ step.completedBy }}
      </div>
      <div v-else class="workflow-step-people">待系统确定处理人</div>
      <div v-if="step.opinion" class="workflow-step-opinion">
        {{ isRejected(step) ? '驳回原因' : '意见' }}：{{ opinionText(step) }}
      </div>
    </ElTimelineItem>
  </ElTimeline>
  <div v-else class="workflow-progress-empty">{{ emptyText }}</div>
</template>

<style scoped src="./WorkflowProgressDetail.css"></style>
