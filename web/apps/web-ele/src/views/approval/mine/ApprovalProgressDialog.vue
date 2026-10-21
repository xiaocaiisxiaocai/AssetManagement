<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';

import { computed } from 'vue';
import { ElDialog } from 'element-plus';

import WorkflowProgressDetail from '#/components/workflow/WorkflowProgressDetail.vue';
import { getWorkflowProgressEmptyText } from '#/components/workflow/workflow-progress-empty-text';

const props = defineProps<{
  selectedFlow?: ApprovalWorkItem;
}>();

const visible = defineModel<boolean>('visible', { default: false });

const emptyText = computed(() => {
  const selectedFlow = props.selectedFlow;
  const directTransfer =
    selectedFlow?.source === 'material' &&
    'directTransfer' in selectedFlow.raw &&
    selectedFlow.raw.directTransfer;
  return getWorkflowProgressEmptyText({
    directTransfer,
    source: selectedFlow?.source,
    status: selectedFlow?.status,
  });
});
</script>

<template>
  <ElDialog v-model="visible" title="申请流转进度" width="620px">
    <WorkflowProgressDetail
      v-if="selectedFlow"
      :empty-text="emptyText"
      :steps="selectedFlow.raw.progressSteps || []"
    />
  </ElDialog>
</template>

<style scoped src="./mine-dialog.css"></style>
