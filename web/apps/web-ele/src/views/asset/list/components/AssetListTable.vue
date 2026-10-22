<script lang="ts" setup>
import type { AssetListQueryState } from '../asset-list-state';

import type { AssetItem, AssetStatus } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';
import type { TableColumnOption } from '#/components/table/column-visibility';
import type { HeaderFilterValue } from '#/components/table/header-multi-filter';

import { computed } from 'vue';

import {
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import {
  deleteFlagsFromStatus,
  deleteStatusFromFlags,
} from '#/components/table/header-multi-filter';
import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { formatDate } from '#/utils/date-format';

const props = defineProps<{
  assets: AssetItem[];
  departmentOptions: { id: number; label: string }[];
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
  userOptionsLoading: boolean;
  users: UserOptionDto[];
}>();

const emit = defineEmits<{
  pageChange: [];
  search: [];
  userSearch: [keyword: string];
}>();

defineSlots<{
  actions?: (props: { row: AssetItem }) => unknown;
}>();

const query = defineModel<AssetListQueryState>('query', { required: true });

const statusGroups = [
  {
    label: '业务状态',
    options: [
      { label: '在库', value: 0 },
      { label: '借出', value: 1 },
    ],
  },
  {
    label: '删除状态',
    options: [
      { label: '未删除', value: 'active' },
      { label: '已删除', value: 'deleted' },
    ],
  },
];

const statusHeader = computed<HeaderFilterValue[]>({
  get: () => [
    ...query.value.statuses,
    ...deleteFlagsFromStatus(query.value.deleteStatus),
  ],
  set: (values) => {
    query.value.statuses = values.filter(
      (value): value is AssetStatus => value === 0 || value === 1,
    );
    query.value.deleteStatus = deleteStatusFromFlags(values);
  },
});

const departmentFilterOptions = computed(() =>
  props.departmentOptions.map((item) => ({
    label: item.label,
    value: item.id,
  })),
);

const custodianFilterOptions = computed(() =>
  props.users.map((user) => ({
    label: `${user.name}（${user.employeeNo}）`,
    value: user.id,
  })),
);

const statusOptions: Array<{
  label: string;
  tag: 'danger' | 'info' | 'success' | 'warning';
  value: AssetStatus;
}> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
];

const tableColumns: TableColumnOption[] = [
  { key: 'assetNo', label: '资产编号' },
  { key: 'name', label: '资产名称' },
  { key: 'department', label: '归属部门' },
  { key: 'location', label: '存放位置' },
  { key: 'custodian', label: '保管人' },
  { key: 'quantity', label: '数量' },
  { key: 'purchaseDate', label: '购入日期' },
  { key: 'registrationTime', label: '资产登记日期' },
  { key: 'condition', label: '目前状况' },
  { key: 'remark', label: '备注' },
  { key: 'photos', label: '照片' },
  { key: 'status', label: '状态' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('asset-list', tableColumns);

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
  <div class="asset-table-panel table-scroll">
    <Teleport defer to="[data-column-tools='asset-list']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable
      :data="assets"
      :row-class-name="tableRowClassName"
      border
      height="100%"
      scrollbar-always-on
      stripe
      v-loading="loading"
    >
      <ElTableColumn
        v-if="visibleColumn('assetNo')"
        label="资产编号"
        min-width="160"
        prop="assetNo"
      />
      <ElTableColumn
        v-if="visibleColumn('name')"
        label="资产名称"
        min-width="180"
        prop="name"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('department')"
        class-name="hide-on-mobile"
        label="归属部门"
        prop="departmentName"
        show-overflow-tooltip
        width="150"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="query.departmentIds"
            :options="departmentFilterOptions"
            filterable
            label="归属部门"
            @change="emit('search')"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('location')"
        class-name="hide-on-mobile"
        label="存放位置"
        prop="locationName"
        show-overflow-tooltip
        width="140"
      />
      <ElTableColumn
        v-if="visibleColumn('custodian')"
        class-name="hide-on-mobile"
        label="保管人"
        prop="custodianName"
        show-overflow-tooltip
        width="130"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="query.custodianIds"
            :loading="userOptionsLoading"
            :options="custodianFilterOptions"
            filterable
            label="保管人"
            remote
            @change="emit('search')"
            @search="emit('userSearch', $event)"
          />
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('quantity')"
        align="center"
        label="数量"
        prop="quantity"
        width="80"
      />
      <ElTableColumn
        v-if="visibleColumn('purchaseDate')"
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
        v-if="visibleColumn('registrationTime')"
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
        v-if="visibleColumn('condition')"
        class-name="hide-on-mobile"
        label="目前状况"
        min-width="160"
        prop="currentCondition"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('remark')"
        class-name="hide-on-mobile"
        label="备注"
        min-width="180"
        prop="remark"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('photos')"
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
      <ElTableColumn
        v-if="visibleColumn('status')"
        align="center"
        label="状态"
        width="120"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="statusHeader"
            :groups="statusGroups"
            label="状态"
            @change="emit('search')"
          />
        </template>
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
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="160"
      >
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
