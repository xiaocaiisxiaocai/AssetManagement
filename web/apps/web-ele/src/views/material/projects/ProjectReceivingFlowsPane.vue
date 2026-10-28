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
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';

defineProps<{
  pageSizeOptions: number[];
  processingFlowIds: number[];
  receivingCount: number;
  receivingFlows: MaterialFlowItem[];
  receivingLoading: boolean;
}>();

const emit = defineEmits<{
  confirm: [flow: MaterialFlowItem];
  pageChange: [];
  receivingPageSizeChange: [];
}>();
const receivingQuery = defineModel<{ page: number; pageSize: number }>(
  'receivingQuery',
  { required: true },
);

const tableColumns: TableColumnOption[] = [
  { key: 'flowNo', label: '流转单号' },
  { key: 'materialNo', label: '料件编号' },
  { key: 'materialName', label: '料件名称' },
  { key: 'applicant', label: '申请人' },
  { key: 'reason', label: '原因' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-flows-receiving', tableColumns);
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
          :data="receivingFlows"
          :empty-text="
            receivingCount > 0 ? '当前页暂无待接收流转' : '暂无待接收流转'
          "
          border
          height="100%"
          stripe
          v-loading="receivingLoading"
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
            min-width="108"
            prop="materialName"
            show-overflow-tooltip
          />
          <ElTableColumn
            v-if="visibleColumn('applicant')"
            class-name="hide-on-mobile"
            label="申请人"
            prop="applicant"
            show-overflow-tooltip
            width="84"
          />
          <ElTableColumn
            v-if="visibleColumn('reason')"
            class-name="hide-on-mobile"
            label="原因"
            min-width="108"
            prop="reason"
            show-overflow-tooltip
          />
          <ElTableColumn
            v-if="visibleColumn('actions')"
            align="center"
            class-name="table-action-cell"
            label="操作"
            width="108"
          >
            <template #default="{ row }">
              <ElButton
                :disabled="processingFlowIds.includes(row.id)"
                link
                size="small"
                type="primary"
                @click="emit('confirm', row)"
              >
                确认接收
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>
      <div
        v-if="receivingCount > receivingQuery.pageSize"
        class="table-bottom-pager"
      >
        <div class="table-bottom-pager-left">
          <span>共 {{ receivingCount }} 条记录</span>
          <span class="table-bottom-pager-divider">|</span>
          <span>每页</span>
          <ElSelect
            v-model="receivingQuery.pageSize"
            style="width: 92px"
            @change="emit('receivingPageSizeChange')"
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
          v-model:current-page="receivingQuery.page"
          :page-size="receivingQuery.pageSize"
          :total="receivingCount"
          background
          layout="prev, pager, next"
          @current-change="emit('pageChange')"
        />
      </div>
    </div>
  </div>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
