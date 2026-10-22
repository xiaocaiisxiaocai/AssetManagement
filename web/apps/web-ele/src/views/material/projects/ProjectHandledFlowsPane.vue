<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';
import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
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

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流转单号' },
  { key: 'materialNo', label: '料件编号' },
  { key: 'materialName', label: '料件名称' },
  { key: 'applicant', label: '发起人' },
  { key: 'transferee', label: '受让人' },
  { key: 'reason', label: '原因' },
  { key: 'progress', label: '审批进度' },
  { key: 'myAction', label: '我的处理' },
  { key: 'myTime', label: '处理时间' },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-flows-handled', tableColumns);
</script>

<template>
  <div class="drawer-table-panel flow-table-panel">
    <ElTable
      :data="handledFlows"
      :empty-text="handledCount > 0 ? '当前页暂无已处理记录' : '暂无已处理记录'"
      border
      height="100%"
      stripe
      v-loading="handledLoading"
    >
      <ElTableColumn
        v-if="visibleColumn('flowNo')"
        label="流转单号"
        min-width="170"
        prop="flowNo"
      />
      <ElTableColumn
        v-if="visibleColumn('materialNo')"
        label="料件编号"
        min-width="150"
        prop="materialNo"
      />
      <ElTableColumn
        v-if="visibleColumn('materialName')"
        label="料件名称"
        min-width="140"
        prop="materialName"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('applicant')"
        class-name="hide-on-mobile"
        label="发起人"
        min-width="90"
        prop="applicant"
      />
      <ElTableColumn
        v-if="visibleColumn('transferee')"
        class-name="hide-on-mobile"
        label="受让人"
        min-width="90"
        prop="transferee"
      />
      <ElTableColumn
        v-if="visibleColumn('reason')"
        class-name="hide-on-mobile"
        label="原因"
        min-width="150"
        prop="reason"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('progress')"
        label="审批进度"
        min-width="320"
      >
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.currentSteps"
            :next-steps="row.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('myAction')"
        align="center"
        label="我的处理"
        width="110"
      >
        <template #default="{ row }">
          <ElTag
            :type="row.myApprovalAction === 'reject' ? 'danger' : 'success'"
            size="small"
          >
            {{ approvalActionLabel(row.myApprovalAction) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('myTime')"
        align="center"
        class-name="hide-on-mobile"
        label="处理时间"
        min-width="160"
      >
        <template #default="{ row }">
          {{ formatDateTime(row.myApprovalTime) }}
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <TableColumnPicker
          :columns="tableColumns"
          :hidden-keys="hiddenKeys"
          @reset="resetColumns"
          @toggle="setColumnVisible"
        />
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
