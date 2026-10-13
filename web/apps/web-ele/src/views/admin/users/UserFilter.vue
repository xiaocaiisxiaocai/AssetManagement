<script lang="ts" setup>
import type { UserListQuery } from './user-form';

import type { RoleDto } from '#/api/role';

import {
  ElButton,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
} from 'element-plus';

import type { DepartmentOption } from '#/utils/department-options';

defineProps<{
  departmentOptions: DepartmentOption[];
  roles: RoleDto[];
}>();

const emit = defineEmits<{
  reset: [];
  search: [];
}>();
const query = defineModel<UserListQuery>('query', { required: true });
</script>

<template>
  <div class="filter-panel">
    <ElForm class="filter-form" inline>
      <ElFormItem label="搜索">
        <ElInput
          v-model="query.keyword"
          clearable
          placeholder="工号或姓名"
          style="width: 240px"
          @keyup.enter="emit('search')"
        />
      </ElFormItem>
      <ElFormItem label="部门">
        <ElSelect
          v-model="query.departmentId"
          clearable
          filterable
          placeholder="全部部门"
          style="width: 200px"
        >
          <ElOption
            v-for="department in departmentOptions"
            :key="department.id"
            :label="department.label"
            :value="department.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="角色">
        <ElSelect
          v-model="query.roleId"
          clearable
          filterable
          placeholder="全部角色"
          style="width: 180px"
        >
          <ElOption
            v-for="role in roles"
            :key="role.id"
            :label="role.name"
            :value="role.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem>
        <ElButton type="primary" @click="emit('search')">查询</ElButton>
        <ElButton @click="emit('reset')">重置</ElButton>
      </ElFormItem>
    </ElForm>
  </div>
</template>

<style scoped src="./UserFilter.css"></style>
