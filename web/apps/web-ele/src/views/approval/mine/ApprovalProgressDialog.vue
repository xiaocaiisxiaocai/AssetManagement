<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';

import { ElDialog } from 'element-plus';

import WorkflowProgressDetail from '#/components/workflow/WorkflowProgressDetail.vue';

defineProps<{
  selectedFlow?: ApprovalWorkItem;
}>();

const visible = defineModel<boolean>('visible', { default: false });
</script>

<template>
  <ElDialog v-model="visible" title="申请流转进度" width="620px">
    <WorkflowProgressDetail
      v-if="selectedFlow"
      :steps="selectedFlow.raw.progressSteps || []"
    />
  </ElDialog>
</template>

<style scoped src="./mine-dialog.css"></style>
