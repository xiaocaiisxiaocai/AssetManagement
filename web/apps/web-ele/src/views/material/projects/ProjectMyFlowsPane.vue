<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';

import { canWithdrawMaterialFlow } from './project-workspace-rules';

defineProps<{
  myCount: number;
  myFlows: MaterialFlowItem[];
  myLoading: boolean;
  pageSizeOptions: number[];
  processingFlowIds: number[];
  readOnly: boolean;
}>();

const emit = defineEmits<{
  myPageSizeChange: [];
  pageChange: [];
  withdraw: [flow: MaterialFlowItem];
}>();
const myQuery = defineModel<{ page: number; pageSize: number }>('myQuery', {
  required: true,
});

const flowStatusMeta: Record<
  string,
  { label: string; tag: 'info' | 'success' | 'warning' }
> = {
  approved: { label: '已通过', tag: 'success' },
  pending: { label: '审批中', tag: 'warning' },
  rejected: { label: '已驳回', tag: 'info' },
  withdrawn: { label: '已撤回', tag: 'info' },
};

function flowMetaOf(status: string) {
  return flowStatusMeta[status] ?? { label: status, tag: 'info' as const };
}
</script>

<template>
  <div class="drawer-table-panel flow-table-panel">
    <ElTable
      :data="myFlows"
      :empty-text="myCount > 0 ? '当前页暂无发起记录' : '暂无发起记录'"
      border
      height="100%"
      stripe
      v-loading="myLoading"
    >
      <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
      <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
      <ElTableColumn
        label="料件名称"
        min-width="140"
        prop="materialName"
        show-overflow-tooltip
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="受让人"
        min-width="90"
        prop="transferee"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="原因"
        min-width="150"
        prop="reason"
        show-overflow-tooltip
      />
      <ElTableColumn align="center" label="状态" width="110">
        <template #default="{ row }">
          <ElTag :type="flowMetaOf(row.status).tag" size="small">
            {{ flowMetaOf(row.status).label }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="审批进度" min-width="320">
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.currentSteps"
            :next-steps="row.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="90"
      >
        <template #default="{ row }">
          <ElButton
            v-if="!readOnly && canWithdrawMaterialFlow(row)"
            :disabled="processingFlowIds.includes(row.id)"
            link
            size="small"
            type="danger"
            @click="emit('withdraw', row)"
          >
            撤回
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ myCount }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="myQuery.pageSize"
          style="width: 92px"
          @change="emit('myPageSizeChange')"
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
        v-model:current-page="myQuery.page"
        :page-size="myQuery.pageSize"
        :total="myCount"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
