<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';
import type { TableColumnOption } from '#/components/table/column-visibility';

import { ElButton, ElTable, ElTableColumn, ElTag } from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';

type CategoryActionAccess = {
  canCreate: boolean;
  canDelete: boolean;
  canEdit: boolean;
  canPurge: boolean;
  canRestore: boolean;
};

defineProps<{
  access: CategoryActionAccess;
  canCreateChild: (row: CategoryNode) => boolean;
  categories: CategoryNode[];
  loading: boolean;
}>();

const emit = defineEmits<{
  create: [row: CategoryNode];
  edit: [row: CategoryNode];
  purge: [row: CategoryNode];
  remove: [row: CategoryNode];
  restore: [row: CategoryNode];
}>();

function tableRowClassName({ row }: { row: CategoryNode }) {
  return row.isDeleted ? 'category-row-deleted' : '';
}

const tableColumns: TableColumnOption[] = [
  { key: 'codeSeg', label: '编码段' },
  { key: 'code', label: '完整编码' },
  { key: 'remark', label: '备注' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('admin-categories', tableColumns);
</script>

<template>
  <div class="table-panel page-fill table-scroll">
    <Teleport defer to="[data-column-tools='admin-categories']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable
      :data="categories"
      :row-class-name="tableRowClassName"
      border
      default-expand-all
      height="100%"
      row-key="id"
      v-loading="loading"
    >
      <ElTableColumn
        v-if="visibleColumn('codeSeg')"
        label="编码段"
        min-width="140"
        prop="codeSeg"
      />
      <ElTableColumn
        v-if="visibleColumn('code')"
        label="完整编码"
        min-width="200"
      >
        <template #default="{ row }">
          <ElTag size="default">{{ row.code }}</ElTag>
          <ElTag v-if="row.isDeleted" class="ml-1" size="small" type="danger">
            已删除
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('remark')"
        class-name="hide-on-mobile"
        label="备注"
        min-width="260"
      >
        <template #default="{ row }">
          <div class="category-remark">
            {{ row.parentId ? row.remark || '-' : '-' }}
          </div>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="260"
      >
        <template #default="{ row }">
          <div v-if="!row.isDeleted" class="category-row-actions">
            <span class="category-row-action">
              <ElButton
                v-if="access.canCreate && canCreateChild(row)"
                link
                size="small"
                type="primary"
                @click="emit('create', row)"
              >
                新增下级
              </ElButton>
            </span>
            <span class="category-row-action">
              <ElButton
                v-if="access.canEdit"
                link
                size="small"
                type="primary"
                @click="emit('edit', row)"
              >
                编辑
              </ElButton>
            </span>
            <span class="category-row-action">
              <ElButton
                v-if="access.canDelete"
                link
                size="small"
                type="danger"
                @click="emit('remove', row)"
              >
                删除
              </ElButton>
            </span>
          </div>
          <div
            v-else-if="access.canRestore || access.canPurge"
            class="category-row-actions"
          >
            <span class="category-row-action">
              <ElButton
                v-if="access.canRestore"
                link
                size="small"
                type="success"
                @click="emit('restore', row)"
              >
                撤销删除
              </ElButton>
            </span>
            <span class="category-row-action">
              <ElButton
                v-if="access.canPurge"
                link
                size="small"
                type="danger"
                @click="emit('purge', row)"
              >
                彻底删除
              </ElButton>
            </span>
            <span class="category-row-action"></span>
          </div>
          <span v-else class="asset-no-permission">无操作权限</span>
        </template>
      </ElTableColumn>
    </ElTable>
  </div>
</template>

<style scoped src="./CategoryTreeTable.css"></style>
