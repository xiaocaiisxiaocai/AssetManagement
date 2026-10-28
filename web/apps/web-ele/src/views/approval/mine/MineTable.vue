<script lang="ts" setup>
import type { ApprovalWorkItem } from '../approval-work-items';
import type { ApprovalSource } from '../notification-flow-attempt';

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
import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';

import { canWithdrawApproval } from '../approval-work-items';

defineProps<{
  activeSource: ApprovalSource;
  flows: ApprovalWorkItem[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
  withdrawingKeys: string[];
}>();

const emit = defineEmits<{
  pageChange: [];
  pageSizeChange: [];
  search: [];
  showProgress: [row: ApprovalWorkItem];
  withdraw: [row: ApprovalWorkItem];
}>();

const query = defineModel<{ page: number; pageSize: number }>('query', {
  required: true,
});
const filters = defineModel<{
  bizTypes: string[];
  statuses: string[];
}>('filters', { required: true });

const bizTypeOptions = [
  { label: '借用', value: 'borrow' },
  { label: '转让', value: 'transfer' },
  { label: '延期', value: 'extension' },
  { label: '归还', value: 'return' },
];
const statusOptions = [
  { label: '审批中', value: 'pending' },
  { label: '待接收', value: 'receiving' },
  { label: '已通过', value: 'approved' },
  { label: '已驳回', value: 'rejected' },
  { label: '已撤回', value: 'withdrawn' },
];

function statusText(status: string) {
  return (
    {
      approved: '已通过',
      pending: '审批中',
      receiving: '待接收',
      rejected: '已驳回',
      withdrawn: '已撤回',
    }[status] ?? status
  );
}

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流程编号' },
  { key: 'source', label: '来源' },
  { key: 'bizType', label: '类型' },
  { key: 'objectName', label: '对象' },
  { key: 'participant', label: '借用人/接收人' },
  { key: 'progress', label: '审批进度' },
  { key: 'status', label: '状态' },
  { key: 'reason', label: '申请事由' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('approval-mine', tableColumns, (key, visible) => {
    if (visible) return;
    let changed = false;
    if (key === 'bizType' && filters.value.bizTypes.length > 0) {
      filters.value.bizTypes = [];
      changed = true;
    }
    if (key === 'status' && filters.value.statuses.length > 0) {
      filters.value.statuses = [];
      changed = true;
    }
    if (changed) emit('search');
  });
</script>

<template>
  <div class="mine-table-panel page-fill table-scroll">
    <Teleport defer to="[data-column-tools='approval-mine']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable :data="flows" border height="100%" v-loading="loading">
      <ElTableColumn
        v-if="visibleColumn('flowNo')"
        label="流程编号"
        min-width="180"
        prop="flowNo"
      />
      <ElTableColumn
        v-if="visibleColumn('source')"
        align="center"
        class-name="hide-on-mobile"
        label="来源"
        width="110"
      >
        <template #default="{ row }">
          <ElTag
            :type="row.source === 'asset' ? 'primary' : 'success'"
            size="small"
          >
            {{ row.sourceLabel }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('bizType')"
        align="center"
        class-name="hide-on-mobile"
        label="类型"
        width="120"
      >
        <template #header>
          <HeaderMultiFilter
            v-if="activeSource === 'asset'"
            v-model="filters.bizTypes"
            :options="bizTypeOptions"
            label="类型"
            @change="emit('search')"
          />
          <span v-else>类型</span>
        </template>
        <template #default="{ row }">
          <ElTag
            :type="
              row.bizType === 'borrow'
                ? 'success'
                : row.bizType === 'extension'
                  ? 'primary'
                  : row.bizType === 'transfer'
                    ? 'warning'
                    : row.bizType === 'material_transfer'
                      ? 'primary'
                      : 'info'
            "
            size="small"
          >
            {{ row.typeLabel }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('objectName')"
        label="对象"
        min-width="180"
        prop="objectName"
      />
      <ElTableColumn
        v-if="visibleColumn('participant')"
        class-name="hide-on-mobile"
        label="借用人/接收人"
        min-width="140"
        prop="participant"
      />
      <ElTableColumn
        v-if="visibleColumn('progress')"
        label="审批进度"
        min-width="310"
      >
        <template #default="{ row }">
          <WorkflowProgressSummary
            :current-steps="row.raw.currentSteps"
            :next-steps="row.raw.nextSteps"
            :status="row.status"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('status')"
        align="center"
        label="状态"
        width="120"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="filters.statuses"
            :options="statusOptions"
            label="状态"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          <ElTag
            :type="
              row.status === 'approved'
                ? 'success'
                : row.status === 'rejected'
                  ? 'danger'
                  : row.status === 'withdrawn'
                    ? 'info'
                    : 'warning'
            "
            size="small"
          >
            {{ statusText(row.status) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('reason')"
        class-name="hide-on-mobile"
        label="申请事由"
        min-width="220"
        prop="reason"
      />
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="180"
      >
        <template #default="{ row }">
          <ElButton link type="primary" @click="emit('showProgress', row)">
            流程
          </ElButton>
          <ElButton
            v-if="row.canReleaseStuck && row.status === 'pending'"
            :loading="withdrawingKeys.includes(row.key)"
            link
            type="danger"
            @click="emit('withdraw', row)"
          >
            终止并重新发起
          </ElButton>
          <ElButton
            v-else-if="canWithdrawApproval(row)"
            :loading="withdrawingKeys.includes(row.key)"
            link
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
        <span>共 {{ total }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="query.pageSize"
          style="width: 92px"
          @change="emit('pageSizeChange')"
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
        v-model:current-page="query.page"
        :page-size="query.pageSize"
        :total="total"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped src="./MineTable.css"></style>
