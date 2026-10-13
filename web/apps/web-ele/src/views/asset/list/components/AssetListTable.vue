<script lang="ts" setup>
import type { AssetListQueryState } from '../asset-list-state';

import type { AssetItem, AssetStatus } from '#/api/asset';

import {
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { formatDate } from '#/utils/date-format';

defineProps<{
  assets: AssetItem[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
}>();

const emit = defineEmits<{
  pageChange: [];
  search: [];
}>();

defineSlots<{
  actions?: (props: { row: AssetItem }) => unknown;
}>();

const query = defineModel<AssetListQueryState>('query', { required: true });

const statusOptions: Array<{
  label: string;
  tag: 'danger' | 'info' | 'success' | 'warning';
  value: AssetStatus;
}> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
];

function statusMeta(status: AssetStatus) {
  return (
    statusOptions.find((item) => item.value === status) ?? {
      label: '未知',
      tag: 'info' as const,
      value: status,
    }
  );
}

function tableRowClassName({ row }: { row: AssetItem }) {
  return row.isDeleted ? 'asset-row-deleted' : '';
}
</script>

<template>
  <div class="asset-table-panel">
    <ElTable
      :data="assets"
      :row-class-name="tableRowClassName"
      border
      height="100%"
      scrollbar-always-on
      stripe
      v-loading="loading"
    >
      <ElTableColumn label="资产编号" min-width="160" prop="assetNo" sortable />
      <ElTableColumn
        label="资产名称"
        min-width="180"
        prop="name"
        show-overflow-tooltip
        sortable
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="归属部门"
        prop="departmentName"
        show-overflow-tooltip
        width="140"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="存放位置"
        prop="locationName"
        show-overflow-tooltip
        width="140"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="保管人"
        prop="custodianName"
        show-overflow-tooltip
        width="110"
      />
      <ElTableColumn align="center" label="数量" prop="quantity" width="80" />
      <ElTableColumn
        align="center"
        class-name="hide-on-mobile"
        label="购入日期"
        width="120"
      >
        <template #default="{ row }">
          {{ formatDate(row.purchaseDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        align="center"
        class-name="hide-on-mobile"
        label="资产登记日期"
        width="120"
      >
        <template #default="{ row }">
          {{ formatDate(row.registrationTime) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="目前状况"
        min-width="160"
        prop="currentCondition"
        show-overflow-tooltip
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="备注"
        min-width="180"
        prop="remark"
        show-overflow-tooltip
      />
      <ElTableColumn
        align="center"
        class-name="hide-on-mobile"
        label="照片"
        width="80"
      >
        <template #default="{ row }">
          <ElTag
            v-if="row.images && row.images.length > 0"
            size="small"
            type="success"
          >
            {{ row.images.length }}
          </ElTag>
          <span v-else class="text-gray-400">-</span>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="状态" width="90">
        <template #default="{ row }">
          <div class="asset-status-tags">
            <ElTag :type="statusMeta(row.status).tag" size="small">
              {{ statusMeta(row.status).label }}
            </ElTag>
            <ElTag v-if="row.isDeleted" size="small" type="danger">
              已删除
            </ElTag>
          </div>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" fixed="right" label="操作" width="160">
        <template #default="{ row }">
          <slot :row="row" name="actions"></slot>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="asset-pager">
      <div class="asset-pager-left">
        <span>共 {{ total }} 条记录</span>
        <span class="asset-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="query.pageSize"
          style="width: 92px"
          @change="emit('search')"
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

<style scoped src="./AssetTablePanel.css"></style>
