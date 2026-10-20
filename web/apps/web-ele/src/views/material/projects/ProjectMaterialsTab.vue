<script lang="ts" setup>
import type { DeleteStatus } from './project-workspace-types';

import type { MaterialItem, MaterialStatus } from '#/api/material';
import type { HeaderFilterValue } from '#/components/table/header-multi-filter';

import { computed } from 'vue';

import {
  ElButton,
  ElOption,
  ElEmpty,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  deleteFlagsFromStatus,
  deleteStatusFromFlags,
} from '#/components/table/header-multi-filter';
import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';

import ProjectMaterialFilterBar from './ProjectMaterialFilterBar.vue';
import ProjectMaterialRowActions from './ProjectMaterialRowActions.vue';

type MaterialActionAccess = {
  canDelete: boolean;
  canPurge: boolean;
  canRestore: boolean;
  canReturn: boolean;
  canTransfer: boolean;
};

const props = defineProps<{
  access: MaterialActionAccess;
  canCreate: boolean;
  canEdit: boolean;
  currentUserId: number;
  isSupervisor: boolean;
  loading: boolean;
  materials: MaterialItem[];
  pageSizeOptions: number[];
  projectOwnerId?: null | number;
  readOnlyHint?: string;
  total: number;
}>();

const emit = defineEmits<{
  command: [command: number | string, material: MaterialItem];
  create: [];
  detail: [material: MaterialItem];
  edit: [material: MaterialItem];
  pageChange: [];
  pageSizeChange: [];
  reset: [];
  search: [];
}>();
const query = defineModel<{
  deleteStatus: DeleteStatus;
  materialNo: string;
  name: string;
  page: number;
  pageSize: number;
  statuses: MaterialStatus[];
}>('query', { required: true });

const statusOptions: Array<{
  label: string;
  tag: 'info' | 'success';
  value: MaterialStatus;
}> = [
  { label: '在用', tag: 'success', value: 0 },
  { label: '已退回厂商', tag: 'info', value: 1 },
];

const statusHeader = computed<HeaderFilterValue[]>({
  get: () => [
    ...query.value.statuses,
    ...deleteFlagsFromStatus(query.value.deleteStatus),
  ],
  set: (values) => {
    query.value.statuses = values.filter(
      (value): value is MaterialStatus => value === 0 || value === 1,
    );
    query.value.deleteStatus = deleteStatusFromFlags(values);
  },
});
const statusGroups = [
  {
    label: '业务状态',
    options: [
      { label: '在用', value: 0 },
      { label: '已退回厂商', value: 1 },
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

function statusMeta(status: MaterialStatus) {
  return (
    statusOptions.find((item) => item.value === status) ?? statusOptions[0]!
  );
}

function rowClassName({ row }: { row: MaterialItem }) {
  if (row.isDeleted) return 'material-row-deleted';
  return row.status === 1 ? 'material-row-returned' : '';
}

const hasMaterialFilters = computed(() => {
  return Boolean(
    query.value.statuses.length > 0 || query.value.deleteStatus !== 'all',
  );
});

const materialEmptyText = computed(() => {
  const hasFilters = hasMaterialFilters.value;
  return hasFilters
    ? '没有符合条件的料件，请清空筛选后重试'
    : props.canCreate
      ? '暂无料件，可新增料件'
      : '暂无料件';
});
</script>

<template>
  <section aria-label="项目料件清单" class="workspace-materials">
    <div class="material-section-heading">
      <h3>
        料件清单 <span class="tab-count">{{ total }}</span>
      </h3>
      <span class="material-section-hint">项目测试使用的样品与物料</span>
    </div>
    <ProjectMaterialFilterBar
      :can-create="canCreate"
      :read-only-hint="readOnlyHint"
      @create="emit('create')"
    />
    <div class="drawer-table-panel material-table-panel">
      <ElTable
        :data="materials"
        height="220"
        :row-class-name="rowClassName"
        border
        :empty-text="materialEmptyText"
        scrollbar-always-on
        stripe
        v-loading="loading"
      >
        <template #empty>
          <ElEmpty :description="materialEmptyText">
            <ElButton v-if="hasMaterialFilters" @click="emit('reset')">
              清空筛选
            </ElButton>
          </ElEmpty>
        </template>
        <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
        <ElTableColumn
          label="名称"
          min-width="140"
          prop="name"
          show-overflow-tooltip
        />
        <ElTableColumn
          class-name="hide-on-mobile"
          label="寄件厂商"
          min-width="120"
          prop="vendorName"
          show-overflow-tooltip
        />
        <ElTableColumn
          class-name="hide-on-mobile"
          label="型号品牌"
          min-width="140"
          show-overflow-tooltip
        >
          <template #default="{ row }">
            <span v-if="row.model || row.brand">
              {{ row.model }} {{ row.brand }}
            </span>
            <span v-else>-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="数量" prop="quantity" width="70" />
        <ElTableColumn
          class-name="hide-on-mobile"
          label="对接厂商"
          min-width="120"
          prop="dockingVendorName"
          show-overflow-tooltip
        />
        <ElTableColumn
          class-name="hide-on-mobile"
          label="部门"
          min-width="110"
          prop="departmentName"
          show-overflow-tooltip
        />
        <ElTableColumn
          class-name="hide-on-mobile"
          label="保管人"
          min-width="100"
          prop="custodianName"
          show-overflow-tooltip
        />
        <ElTableColumn
          class-name="hide-on-mobile"
          label="备注"
          min-width="180"
          show-overflow-tooltip
        >
          <template #default="{ row }">{{ row.remark || '-' }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="状态" width="140">
          <template #header>
            <HeaderMultiFilter
              v-model="statusHeader"
              :groups="statusGroups"
              label="状态"
              @change="emit('search')"
            />
          </template>
          <template #default="{ row }">
            <ElTag :type="statusMeta(row.status).tag" size="small">
              {{ statusMeta(row.status).label }}
            </ElTag>
            <ElTag v-if="row.isDeleted" class="ml-1" size="small" type="danger">
              已删除
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn
          align="center"
          class-name="table-action-cell"
          fixed="right"
          label="操作"
          width="150"
        >
          <template #default="{ row }">
            <ProjectMaterialRowActions
              :access="access"
              :can-edit="canEdit"
              :current-user-id="currentUserId"
              :is-supervisor="isSupervisor"
              :project-owner-id="projectOwnerId"
              :row="row"
              @command="(command) => emit('command', command, row)"
              @detail="emit('detail', row)"
              @edit="emit('edit', row)"
            />
          </template>
        </ElTableColumn>
      </ElTable>
      <div v-if="total > 3 || query.page > 1" class="table-bottom-pager">
        <div class="table-bottom-pager-left">
          <span>共 {{ total }} 条记录</span>
          <span class="table-bottom-pager-divider">|</span>
          <span>每页</span>
          <ElSelect
            v-model="query.pageSize"
            aria-label="料件列表每页条数"
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
  </section>
</template>

<style scoped src="./ProjectMaterialsTab.css"></style>
