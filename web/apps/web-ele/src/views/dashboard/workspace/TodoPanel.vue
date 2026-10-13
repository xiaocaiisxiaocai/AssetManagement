<script lang="ts" setup>
import { ElButton, ElCard } from 'element-plus';

defineProps<{
  canConfirmReturns: boolean;
  canHandleAnyApprovals: boolean;
  canHandleApprovals: boolean;
  canViewAnyApplications: boolean;
  canViewReports: boolean;
  myApplicationTarget: null | string;
  overdueTotal: null | number;
  pendingApprovalCount: null | number;
  pendingApprovalTarget: null | string;
  pendingMineCount: null | number;
  pendingReturns: null | number;
}>();

const emit = defineEmits<{
  navigate: [path: null | string];
}>();

function formatCount(value: null | number) {
  return value ?? '—';
}
</script>

<template>
  <ElCard class="workspace-panel workspace-todo-panel" shadow="never">
    <template #header>
      <div class="workspace-card-header">
        <span>待办提醒</span>
        <ElButton
          v-if="canHandleApprovals"
          link
          type="primary"
          @click="emit('navigate', '/approval/pending')"
        >
          处理审批
        </ElButton>
      </div>
    </template>
    <div class="workspace-todo-list">
      <button
        v-if="canHandleAnyApprovals"
        class="workspace-todo-item"
        type="button"
        @click="emit('navigate', pendingApprovalTarget)"
      >
        <span>待我审批</span>
        <strong>{{ formatCount(pendingApprovalCount) }}</strong>
      </button>
      <button
        v-if="canViewAnyApplications"
        class="workspace-todo-item"
        type="button"
        @click="emit('navigate', myApplicationTarget)"
      >
        <span>我的审批中申请</span>
        <strong>{{ formatCount(pendingMineCount) }}</strong>
      </button>
      <button
        v-if="canConfirmReturns"
        class="workspace-todo-item"
        type="button"
        @click="emit('navigate', '/approval/confirm-return')"
      >
        <span>待确认归还</span>
        <strong>{{ formatCount(pendingReturns) }}</strong>
      </button>
      <button
        v-if="canViewReports"
        class="workspace-todo-item"
        type="button"
        @click="emit('navigate', '/report/overdue')"
      >
        <span>逾期未归还</span>
        <strong>{{ formatCount(overdueTotal) }}</strong>
      </button>
    </div>
  </ElCard>
</template>

<style scoped src="./workspace-panel.css"></style>
<style scoped src="./TodoPanel.css"></style>
