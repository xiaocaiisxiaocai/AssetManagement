<script lang="ts" setup>
import type { ApprovalFlow } from '#/api/workflow';
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
import { formatDateTime } from '#/utils/date-format';

defineProps<{
  confirmingIds: Set<number>;
  flows: ApprovalFlow[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
}>();

const emit = defineEmits<{
  confirm: [row: ApprovalFlow];
  pageChange: [];
  pageSizeChange: [];
}>();

const query = defineModel<{
  keyword: string;
  page: number;
  pageSize: number;
  returnDate: string;
}>('query', { required: true });

function bizText(type: string) {
  return (
    { borrow: '借用', extension: '延期', return: '归还', transfer: '转让' }[
      type
    ] ?? type
  );
}

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流程编号' },
  { key: 'assetNo', label: '资产编号' },
  { key: 'assetName', label: '资产名称' },
  { key: 'bizType', label: '业务类型' },
  { key: 'applicant', label: '借用人' },
  { key: 'department', label: '借用部门' },
  { key: 'returnDate', label: '应归还日期' },
  { key: 'applyTime', label: '申请时间' },
  { key: 'reason', label: '借用事由' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('approval-confirm-return', tableColumns);
</script>

<template>
  <div class="table-panel-with-toolbar page-fill table-scroll">
    <Teleport defer to="[data-column-tools='approval-confirm-return']">
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
        min-width="160"
        prop="flowNo"
      />
      <ElTableColumn
        v-if="visibleColumn('assetNo')"
        class-name="hide-on-mobile"
        label="资产编号"
        min-width="140"
        prop="assetNo"
      />
      <ElTableColumn
        v-if="visibleColumn('assetName')"
        label="资产名称"
        min-width="200"
        prop="assetName"
      />
      <ElTableColumn
        v-if="visibleColumn('bizType')"
        align="center"
        class-name="hide-on-mobile"
        label="业务类型"
        width="100"
      >
        <template #default="{ row }">
          <ElTag size="small" type="success">
            {{ bizText(row.bizType) }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('applicant')"
        label="借用人"
        min-width="130"
        prop="applicant"
      />
      <ElTableColumn
        v-if="visibleColumn('department')"
        class-name="hide-on-mobile"
        label="借用部门"
        min-width="150"
        prop="applicantDept"
      />
      <ElTableColumn
        v-if="visibleColumn('returnDate')"
        class-name="hide-on-mobile"
        label="应归还日期"
        min-width="140"
        prop="returnDate"
      />
      <ElTableColumn
        v-if="visibleColumn('applyTime')"
        class-name="hide-on-mobile"
        label="申请时间"
        min-width="180"
      >
        <template #default="{ row }">
          {{ formatDateTime(row.applyTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('reason')"
        class-name="hide-on-mobile"
        label="借用事由"
        min-width="200"
        prop="reason"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="120"
      >
        <template #default="{ row }">
          <ElButton
            :loading="confirmingIds.has(row.id)"
            link
            size="small"
            type="primary"
            @click="emit('confirm', row)"
          >
            确认已收回
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
