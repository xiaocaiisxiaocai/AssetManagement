<script lang="ts" setup>
import type { DepartmentNode } from '#/api/base-data';
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
import { buildDepartmentActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildDepartmentActionAccess>;
  canCreateChild: (row: DepartmentNode) => boolean;
  loading: boolean;
  pagedDepartments: DepartmentNode[];
  pageSizeOptions: number[];
  total: number;
}>();

const emit = defineEmits<{
  createChild: [row: DepartmentNode];
  edit: [row: DepartmentNode];
  pageSizeChange: [];
  remove: [row: DepartmentNode];
}>();
const page = defineModel<number>('page', { required: true });
const pageSize = defineModel<number>('pageSize', { required: true });

const tableColumns: TableColumnOption[] = [
  { key: 'id', label: 'ID' },
  { key: 'name', label: '组织架构名称' },
  { key: 'level', label: '组织层级' },
  { key: 'manager', label: '负责人' },
  { key: 'status', label: '状态' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('admin-departments', tableColumns);
</script>

<template>
  <div class="table-panel page-fill table-scroll">
    <Teleport defer to="[data-column-tools='admin-departments']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable
      :data="pagedDepartments"
      border
      default-expand-all
      height="100%"
      row-key="id"
      v-loading="loading"
    >
      <ElTableColumn
        v-if="visibleColumn('id')"
        align="center"
        label="ID"
        prop="id"
        width="90"
      />
      <ElTableColumn
        v-if="visibleColumn('name')"
        label="组织架构名称"
        min-width="200"
        prop="name"
      />
      <ElTableColumn
        v-if="visibleColumn('level')"
        label="组织层级"
        min-width="120"
      >
        <template #default="{ row }">
          <ElTag size="small" type="info">
            {{ row.organizationLevelName || '未配置' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('manager')"
        class-name="hide-on-mobile"
        label="负责人"
        min-width="140"
        prop="managerName"
      />
      <ElTableColumn
        v-if="visibleColumn('status')"
        align="center"
        label="状态"
        min-width="100"
      >
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="admin-action-cell table-action-cell"
        fixed="right"
        label="操作"
        width="170"
      >
        <template #default="{ row }">
          <ElButton
            v-if="access.canCreate && canCreateChild(row)"
            link
            size="small"
            type="primary"
            @click="emit('createChild', row)"
          >
            新增下级
          </ElButton>
          <ElButton
            v-if="access.canEdit"
            link
            size="small"
            type="primary"
            @click="emit('edit', row)"
          >
            编辑
          </ElButton>
          <ElButton
            v-if="access.canDelete"
            link
            size="small"
            type="danger"
            @click="emit('remove', row)"
          >
            删除
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ total }} 条</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="pageSize"
          style="width: 92px"
          @change="emit('pageSizeChange')"
        >
          <ElOption
            v-for="size in pageSizeOptions"
            :key="size"
            :label="`${size} 条`"
            :value="size"
          />
        </ElSelect>
      </div>
      <ElPagination
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        background
        layout="prev, pager, next, jumper"
      />
    </div>
  </div>
</template>

<style scoped>
.table-panel :deep(.admin-action-cell .cell) {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
  justify-content: center;
  white-space: normal;
}

.table-panel :deep(.admin-action-cell .el-button + .el-button) {
  margin-left: 0;
}
</style>
