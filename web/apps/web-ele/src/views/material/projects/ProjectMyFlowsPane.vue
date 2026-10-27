<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';
import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElButton,
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

import {
  canReleaseMaterialFlow,
  canWithdrawMaterialFlow,
} from './project-workspace-rules';

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

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流转单号' },
  { key: 'materialNo', label: '料件编号' },
  { key: 'materialName', label: '料件名称' },
  { key: 'transferee', label: '受让人' },
  { key: 'reason', label: '原因' },
  { key: 'status', label: '状态' },
  { key: 'progress', label: '审批进度' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-flows-mine', tableColumns);
</script>

<template>
  <div class="flow-pane">
    <Teleport defer to="[data-column-tools='project-flows']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        size="small"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <div class="drawer-table-panel flow-table-panel">
      <div class="flow-fill-table">
        <ElTable
          :data="myFlows"
          :empty-text="myCount > 0 ? '当前页暂无发起记录' : '暂无发起记录'"
          border
          height="100%"
          stripe
          v-loading="myLoading"
        >
          <ElTableColumn
            v-if="visibleColumn('flowNo')"
            label="流转单号"
            prop="flowNo"
            show-overflow-tooltip
            width="148"
          />
          <ElTableColumn
            v-if="visibleColumn('materialNo')"
            label="料件编号"
            prop="materialNo"
            show-overflow-tooltip
            width="148"
          />
          <ElTableColumn
            v-if="visibleColumn('materialName')"
            label="料件名称"
            min-width="104"
            prop="materialName"
            show-overflow-tooltip
          />
          <ElTableColumn
            v-if="visibleColumn('transferee')"
            class-name="hide-on-mobile"
            label="受让人"
            prop="transferee"
            show-overflow-tooltip
            width="84"
          />
          <ElTableColumn
            v-if="visibleColumn('reason')"
            class-name="hide-on-mobile"
            label="原因"
            min-width="104"
            prop="reason"
            show-overflow-tooltip
          />
          <ElTableColumn
            v-if="visibleColumn('status')"
            align="center"
            label="状态"
            width="84"
          >
            <template #default="{ row }">
              <ElTag :type="flowMetaOf(row.status).tag" size="small">
                {{ flowMetaOf(row.status).label }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            v-if="visibleColumn('progress')"
            label="审批进度"
            width="168"
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
            v-if="visibleColumn('actions')"
            align="center"
            class-name="table-action-cell"
            label="操作"
            width="128"
          >
            <template #default="{ row }">
              <ElButton
                v-if="!readOnly && canReleaseMaterialFlow(row)"
                :disabled="processingFlowIds.includes(row.id)"
                link
                size="small"
                type="danger"
                @click="emit('withdraw', row)"
              >
                终止并重新发起
              </ElButton>
              <ElButton
                v-else-if="!readOnly && canWithdrawMaterialFlow(row)"
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
      </div>
      <div v-if="myCount > myQuery.pageSize" class="table-bottom-pager">
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
  </div>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
