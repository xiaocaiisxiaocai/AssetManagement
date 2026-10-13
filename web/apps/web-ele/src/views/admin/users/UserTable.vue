<script lang="ts" setup>
import type { UserListQuery } from './user-form';

import type { UserDto } from '#/api/user';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { buildUserActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildUserActionAccess>;
  loading: boolean;
  pageSizeOptions: number[];
  total: number;
  users: UserDto[];
}>();

const emit = defineEmits<{
  edit: [row: UserDto];
  pageChange: [];
  remove: [row: UserDto];
  resetPassword: [row: UserDto];
  search: [];
  toggleStatus: [row: UserDto];
}>();
const query = defineModel<UserListQuery>('query', { required: true });
</script>

<template>
  <div class="user-table-panel">
    <ElTable :data="users" border height="100%" v-loading="loading">
      <ElTableColumn label="工号" min-width="120" prop="employeeNo" />
      <ElTableColumn label="姓名" min-width="140" prop="name" />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="部门"
        min-width="150"
        prop="departmentName"
        show-overflow-tooltip
      >
        <template #default="{ row }">
          {{ row.departmentName || '--' }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        class-name="hide-on-mobile"
        label="邮箱"
        min-width="180"
        prop="email"
      />
      <ElTableColumn
        class-name="hide-on-mobile"
        label="角色"
        min-width="180"
      >
        <template #default="{ row }">
          <template v-if="row.roleNames && row.roleNames.length > 0">
            <ElTag
              v-for="role in row.roleNames"
              :key="role"
              size="small"
              style="margin-right: 4px"
            >
              {{ role }}
            </ElTag>
          </template>
          <span v-else class="user-empty-text">--</span>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="状态" width="100">
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
            {{ row.isActive ? '启用' : '禁用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" fixed="right" label="操作" width="300">
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
            v-if="access.canResetPassword"
            link
            size="small"
            type="primary"
            @click="emit('resetPassword', row)"
          >
            重置密码
          </ElButton>
          <ElButton
            v-if="access.canToggleStatus"
            :disabled="loading"
            :type="row.isActive ? 'danger' : 'primary'"
            link
            size="small"
            @click="emit('toggleStatus', row)"
          >
            {{ row.isActive ? '禁用' : '启用' }}
          </ElButton>
          <ElButton
            v-if="access.canDelete && row.canManage"
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
          aria-label="用户列表每页条数"
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

<style scoped src="./UserTable.css"></style>
