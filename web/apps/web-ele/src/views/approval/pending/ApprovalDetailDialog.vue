<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';

import {
  ElButton,
  ElDescriptions,
  ElDescriptionsItem,
  ElDialog,
  ElInput,
  ElOption,
  ElSelect,
  ElTag,
} from 'element-plus';

import WorkflowProgressDetail from '#/components/workflow/WorkflowProgressDetail.vue';
import { getWorkflowProgressEmptyText } from '#/components/workflow/workflow-progress-empty-text';
import { formatDate, formatDateTime } from '#/utils/date-format';

defineProps<{
  actionLoading: boolean;
  activeNodeOptions: { nodeId: string; nodeName: string }[];
  addSignLoading: boolean;
  canCurrentUserAddSign: boolean;
  cancelAddSignLoadingKey: string;
  currentNodeInfo: {
    count: number;
    names: string;
    nodeIds: string[];
  } | null;
  currentSignStates: {
    canCancel: boolean;
    displayName: string;
    name: string;
    nodeId: string;
    nodeName: string;
    signed: boolean;
  }[];
  selected: ApprovalFlow | null;
}>();

const emit = defineEmits<{
  approve: [];
  cancelAddSign: [
    item: { displayName: string; name: string; nodeId: string },
  ];
  openAddSign: [];
  reject: [];
}>();

const visible = defineModel<boolean>('visible', { default: false });
const opinion = defineModel<string>('opinion', { required: true });
const selectedNodeId = defineModel<string>('selectedNodeId', {
  required: true,
});

function getBizTypeLabel(type: string) {
  const map: Record<string, string> = {
    borrow: '借用',
    extension: '延期',
    transfer: '转让',
    return: '归还',
    material_transfer: '测试料件流转',
  };
  return map[type] || type;
}

function progressEmptyText(flow: ApprovalFlow | null) {
  return getWorkflowProgressEmptyText({
    source: flow?.bizType === 'material_transfer' ? 'material' : 'asset',
    status: flow?.status,
  });
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="false"
    align-center
    class="pending-approval-dialog"
    title="审批"
    width="800px"
  >
    <ElDescriptions v-if="selected" :column="2" border label-width="112px">
      <ElDescriptionsItem label="流程单号">
        {{ selected.flowNo }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="业务类型">
        {{ getBizTypeLabel(selected.bizType) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="资产编号">
        {{ selected.assetNo }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="资产名称">
        {{ selected.assetName }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="申请人">
        {{ selected.applicant }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="申请部门">
        {{ selected.applicantDept || '-' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem :span="2" label="申请时间">
        {{ formatDateTime(selected.applyTime) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem :span="2" label="申请理由">
        {{ selected.reason || '-' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem
        v-if="selected.bizType === 'borrow'"
        :span="2"
        label="归还日期"
      >
        {{ formatDate(selected.returnDate) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem
        v-if="selected.bizType === 'extension'"
        label="原应归还日期"
      >
        {{ formatDate(selected.originalReturnDate) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem
        v-if="selected.bizType === 'extension'"
        label="新归还日期"
      >
        {{ formatDate(selected.returnDate) }}
      </ElDescriptionsItem>
      <ElDescriptionsItem v-if="currentNodeInfo" :span="2" label="当前节点">
        {{ currentNodeInfo.names }}
        <span v-if="currentNodeInfo.count > 1" class="pending-node-tip">
          ({{ currentNodeInfo.count }} 个并行节点)
        </span>
      </ElDescriptionsItem>
      <ElDescriptionsItem :span="2" label="完整流转">
        <WorkflowProgressDetail
          :empty-text="progressEmptyText(selected)"
          :steps="selected.progressSteps || []"
        />
      </ElDescriptionsItem>
      <ElDescriptionsItem
        v-if="activeNodeOptions.length > 1"
        :span="2"
        label="处理节点"
      >
        <ElSelect
          v-model="selectedNodeId"
          placeholder="请选择要处理的节点"
          style="width: 100%"
        >
          <ElOption
            v-for="item in activeNodeOptions"
            :key="item.nodeId"
            :label="item.nodeName"
            :value="item.nodeId"
          />
        </ElSelect>
      </ElDescriptionsItem>
      <ElDescriptionsItem
        v-if="currentSignStates.length > 0"
        :span="2"
        label="签核状态"
      >
        <div class="pending-sign-list">
          <span
            v-for="item in currentSignStates"
            :key="`${item.nodeId}-${item.name}`"
            class="pending-sign-item"
          >
            <ElTag :type="item.signed ? 'success' : 'warning'" size="small">
              {{ item.displayName }} {{ item.signed ? '已签' : '待签' }}
            </ElTag>
            <ElButton
              v-if="item.canCancel"
              :loading="
                cancelAddSignLoadingKey === `${item.nodeId}-${item.name}`
              "
              link
              size="small"
              type="danger"
              @click="emit('cancelAddSign', item)"
            >
              取消加签
            </ElButton>
          </span>
        </div>
      </ElDescriptionsItem>
    </ElDescriptions>

    <div class="pending-opinion-panel">
      <ElInput
        v-model="opinion"
        :maxlength="500"
        :rows="3"
        placeholder="审批意见最多 380 字，驳回理由最多 500 字"
        show-word-limit
        type="textarea"
      />
    </div>

    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton
        v-if="canCurrentUserAddSign"
        :disabled="addSignLoading"
        :loading="addSignLoading"
        @click="emit('openAddSign')"
      >
        加签
      </ElButton>
      <ElButton
        :disabled="actionLoading"
        :loading="actionLoading"
        type="danger"
        @click="emit('reject')"
      >
        驳回
      </ElButton>
      <ElButton
        :disabled="actionLoading"
        :loading="actionLoading"
        type="primary"
        @click="emit('approve')"
      >
        通过
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./ApprovalDetailDialog.css"></style>
<style scoped src="./pending-approval-dialog.css"></style>
