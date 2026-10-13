<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';

import { ElButton, ElTable, ElTableColumn, ElTag } from 'element-plus';

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
</script>

<template>
  <div class="table-panel">
    <ElTable
      :data="categories"
      :row-class-name="tableRowClassName"
      border
      default-expand-all
      height="100%"
      row-key="id"
      v-loading="loading"
    >
      <ElTableColumn label="编码段" min-width="140" prop="codeSeg" />
      <ElTableColumn label="完整编码" min-width="200">
        <template #default="{ row }">
          <ElTag size="default">{{ row.code }}</ElTag>
          <ElTag v-if="row.isDeleted" class="ml-1" size="small" type="danger">
            已删除
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn class-name="hide-on-mobile" label="备注" min-width="260">
        <template #default="{ row }">
          <div class="category-remark">
            {{ row.parentId ? row.remark || '-' : '-' }}
          </div>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" fixed="right" label="操作" width="260">
        <template #default="{ row }">
          <template v-if="!row.isDeleted">
            <ElButton
              v-if="access.canCreate && canCreateChild(row)"
              link
              size="small"
              type="primary"
              @click="emit('create', row)"
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
          <template v-else>
            <ElButton
              v-if="access.canRestore"
              link
              size="small"
              type="success"
              @click="emit('restore', row)"
            >
              撤销删除
            </ElButton>
            <ElButton
              v-if="access.canPurge"
              link
              size="small"
              type="danger"
              @click="emit('purge', row)"
            >
              彻底删除
            </ElButton>
            <span
              v-if="!access.canRestore && !access.canPurge"
              class="asset-no-permission"
            >
              无操作权限
            </span>
          </template>
        </template>
      </ElTableColumn>
    </ElTable>
  </div>
</template>

<style scoped src="./CategoryTreeTable.css"></style>
