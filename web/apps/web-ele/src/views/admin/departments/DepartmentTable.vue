<script lang="ts" setup>
import type { DepartmentNode } from '#/api/base-data';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { buildDepartmentActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildDepartmentActionAccess>;
  canCreateChild: (row: DepartmentNode) => boolean;
  loading: boolean;
  pageSizeOptions: number[];
  pagedDepartments: DepartmentNode[];
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
</script>

<template>
  <div class="table-panel">
    <ElTable
      :data="pagedDepartments"
      border
      default-expand-all
      height="100%"
      row-key="id"
      v-loading="loading"
    >
      <ElTableColumn align="center" label="ID" prop="id" width="90" />
      <ElTableColumn label="组织架构名称" min-width="200" prop="name" />
      <ElTableColumn label="组织层级" min-width="120">
        <template #default="{ row }">
          <ElTag size="small" type="info">
            {{ row.organizationLevelName || '未配置' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="负责人"
        min-width="140"
        prop="managerName"
      />
      <ElTableColumn align="center" label="状态" min-width="100">
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" fixed="right" label="操作" width="240">
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
