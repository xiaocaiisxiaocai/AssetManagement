<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import {
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';
import { formatDateTime } from '#/utils/date-format';

defineProps<{
  handledCount: number;
  handledFlows: MaterialFlowItem[];
  handledLoading: boolean;
  pageSizeOptions: number[];
}>();

const emit = defineEmits<{
  handledPageSizeChange: [];
  pageChange: [];
}>();
const handledQuery = defineModel<{ page: number; pageSize: number }>(
  'handledQuery',
  { required: true },
);

function approvalActionLabel(action?: null | string) {
  return action === 'reject' ? '已驳回' : '已通过';
}
</script>

<template>
  <div class="drawer-table-panel flow-table-panel">
    <ElTable
      :data="handledFlows"
      border
      height="100%"
      stripe
      v-loading="handledLoading"
    >
      <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
      <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
      <ElTableColumn
        label="料件名称"
        min-width="140"
        prop="materialName"
        show-overflow-tooltip
      />
      <ElTableColumn label="发起人" min-width="90" prop="applicant" />
      <ElTableColumn label="受让人" min-width="90" prop="transferee" />
      <ElTableColumn
        label="原因"
        min-width="150"
        prop="reason"
        show-overflow-tooltip
      />
      <ElTableColumn label="审批进度" min-width="320">
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.currentSteps"
            :next-steps="row.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="我的处理" width="110">
        <template #default="{ row }">
          <ElTag
            :type="row.myApprovalAction === 'reject' ? 'danger' : 'success'"
            size="small"
          >
            {{ approvalActionLabel(row.myApprovalAction) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="处理时间" min-width="160">
        <template #default="{ row }">
          {{ formatDateTime(row.myApprovalTime) }}
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ handledCount }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="handledQuery.pageSize"
          style="width: 92px"
          @change="emit('handledPageSizeChange')"
        >
          <ElOption
            v-for="size in pageSizeOptions"
            :key="size"
            :label="`${size}`"
            :value="size"
          />
        </ElSelect>
      </div>
      <ElPagination
        v-model:current-page="handledQuery.page"
        :page-size="handledQuery.pageSize"
        :total="handledCount"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
