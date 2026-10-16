<script lang="ts" setup>
import type { RoleListQuery } from './role-form';

import type { RoleDto } from '#/api/role';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { buildRoleActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildRoleActionAccess>;
  canConfigureAccess: boolean;
  loading: boolean;
  pageSizeOptions: number[];
  roles: RoleDto[];
  total: number;
}>();

const emit = defineEmits<{
  access: [row: RoleDto];
  edit: [row: RoleDto];
  pageChange: [];
  remove: [row: RoleDto];
  search: [];
}>();
const query = defineModel<RoleListQuery>('query', { required: true });
</script>

<template>
  <div class="role-table-panel page-fill table-scroll">
    <ElTable :data="roles" border height="100%" v-loading="loading">
      <ElTableColumn label="角色编码" min-width="130" prop="code" />
      <ElTableColumn label="角色名称" min-width="160" prop="name" />
      <ElTableColumn align="center" label="权限数" width="100">
        <template #default="{ row }">
          {{ row.permissionIds?.length ?? 0 }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        align="center"
        class-name="hide-on-mobile"
        label="菜单数"
        width="100"
      >
        <template #default="{ row }">
          {{ row.menuIds?.length ?? 0 }}
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="状态" width="100">
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
            {{ row.isActive ? '启用' : '禁用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" fixed="right" label="操作" width="250">
        <template #default="{ row }">
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
            v-if="canConfigureAccess"
            link
            size="small"
            type="primary"
            @click="emit('access', row)"
          >
            授权配置
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
        <span>共 {{ total }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="query.pageSize"
          aria-label="角色列表每页条数"
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

<style scoped src="./RoleTable.css"></style>
