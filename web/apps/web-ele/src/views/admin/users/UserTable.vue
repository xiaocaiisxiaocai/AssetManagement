<script lang="ts" setup>
import type { UserListQuery } from './user-form';

import type { RoleDto } from '#/api/role';
import type { UserDto } from '#/api/user';
import type { TableColumnOption } from '#/components/table/column-visibility';
import type { DepartmentOption } from '#/utils/department-options';

import { computed } from 'vue';

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
import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { buildUserActionAccess } from '#/views/permissions/action-access';

const props = defineProps<{
  access: ReturnType<typeof buildUserActionAccess>;
  departmentOptions: DepartmentOption[];
  loading: boolean;
  pageSizeOptions: number[];
  roles: RoleDto[];
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

const departmentFilterOptions = computed(() =>
  props.departmentOptions.map((item) => ({
    label: item.label.trim(),
    value: item.id,
  })),
);
const roleFilterOptions = computed(() =>
  props.roles.map((role) => ({ label: role.name, value: role.id })),
);

const tableColumns: TableColumnOption[] = [
  { key: 'employeeNo', label: '工号' },
  { key: 'name', label: '姓名' },
  { key: 'department', label: '部门' },
  { key: 'email', label: '邮箱' },
  { key: 'roles', label: '角色' },
  { key: 'status', label: '状态' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('admin-users', tableColumns);
</script>

<template>
  <div class="user-table-panel page-fill table-scroll">
    <Teleport defer to="[data-column-tools='admin-users']">
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </Teleport>
    <ElTable :data="users" border height="100%" v-loading="loading">
      <ElTableColumn
        v-if="visibleColumn('employeeNo')"
        label="工号"
        min-width="120"
        prop="employeeNo"
      />
      <ElTableColumn
        v-if="visibleColumn('name')"
        label="姓名"
        min-width="140"
        prop="name"
      />
      <ElTableColumn
        v-if="visibleColumn('department')"
        class-name="hide-on-mobile"
        label="部门"
        min-width="150"
        prop="departmentName"
        show-overflow-tooltip
      >
        <template #header>
          <HeaderMultiFilter
            v-model="query.departmentIds"
            :options="departmentFilterOptions"
            filterable
            label="部门"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          {{ row.departmentName || '--' }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('email')"
        class-name="hide-on-mobile"
        label="邮箱"
        min-width="180"
        prop="email"
      />
      <ElTableColumn
        v-if="visibleColumn('roles')"
        class-name="hide-on-mobile"
        label="角色"
        min-width="180"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="query.roleIds"
            :options="roleFilterOptions"
            filterable
            label="角色"
            @change="emit('search')"
          />
        </template>
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
      <ElTableColumn
        v-if="visibleColumn('status')"
        align="center"
        label="状态"
        width="100"
      >
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
            {{ row.isActive ? '启用' : '禁用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="admin-action-cell table-action-cell"
        fixed="right"
        label="操作"
        width="180"
      >
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
