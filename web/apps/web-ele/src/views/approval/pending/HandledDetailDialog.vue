<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';

import {
  ElButton,
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElTag,
} from 'element-plus';

import WorkflowProgressDetail from '#/components/workflow/WorkflowProgressDetail.vue';
import { formatDateTime } from '#/utils/date-format';

defineProps<{
  handledSelected: ApprovalWorkItem | null;
}>();

const visible = defineModel<boolean>('visible', { default: false });

function approvalActionLabel(action: null | string | undefined) {
  return action === 'reject' ? '已驳回' : '已通过';
}

function flowStatusLabel(status: string) {
  return (
    {
      approved: '已通过',
      pending: '流转中',
      rejected: '已驳回',
      withdrawn: '已撤回',
    }[status] || status
  );
}
</script>

<template>
  <ElDialog
    v-model="visible"
    align-center
    class="pending-approval-dialog"
    title="审批记录"
    width="680px"
  >
    <ElDescriptions v-if="handledSelected" :column="2" border>
      <ElDescriptionsItem label="流程单号">
        {{ handledSelected.flowNo }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="当前状态">
        {{ flowStatusLabel(handledSelected.status) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="业务类型">
        {{ handledSelected.typeLabel }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="审批对象">
        {{ handledSelected.objectName }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="申请人">
        {{ handledSelected.applicant }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="我的处理">
        <ElTag
          :type="
            handledSelected.myApprovalAction === 'reject'
              ? 'danger'
              : 'success'
          "
          effect="plain"
          size="small"
        >
          {{ approvalActionLabel(handledSelected.myApprovalAction) }}
        </ElTag>
      </ElDescriptionsItem>
      <ElDescriptionsItem label="申请时间">
        {{ formatDateTime(handledSelected.applyTime) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="处理时间">
        {{ formatDateTime(handledSelected.myApprovalTime) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem :span="2" label="申请理由">
        {{ handledSelected.reason || '-' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem :span="2" label="完整审批记录">
        <WorkflowProgressDetail
          :steps="handledSelected.raw.progressSteps || []"
        />
      </ElDescriptionsItem>
    </ElDescriptions>
    <template #footer>
      <ElButton @click="visible = false">关闭</ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./pending-approval-dialog.css"></style>
