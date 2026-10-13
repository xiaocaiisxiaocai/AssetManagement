<script lang="ts" setup>
import type { UserFormState } from './user-form';

import type { RoleDto } from '#/api/role';
import type { UserOptionDto } from '#/api/user';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
} from 'element-plus';

import type { DepartmentOption } from '#/utils/department-options';

defineProps<{
  canAssignRole: boolean;
  departmentOptions: DepartmentOption[];
  editing: boolean;
  roles: RoleDto[];
  saving: boolean;
  supervisorOptions: UserOptionDto[];
  supervisorOptionsLoading: boolean;
}>();

const emit = defineEmits<{
  departmentChange: [departmentId?: number];
  save: [];
  supervisorSearch: [keyword: string];
}>();
const form = defineModel<UserFormState>('form', { required: true });
const visible = defineModel<boolean>('visible', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="editing ? '编辑用户' : '新增用户'"
    width="540px"
  >
    <ElForm class="user-edit-form" label-width="100px">
      <ElFormItem label="工号" required>
        <ElInput
          v-model="form.employeeNo"
          :disabled="editing"
          maxlength="50"
          placeholder="新增用户时必填"
        />
      </ElFormItem>
      <ElFormItem label="姓名" required>
        <ElInput
          v-model="form.name"
          maxlength="100"
          placeholder="请输入姓名"
        />
      </ElFormItem>
      <ElFormItem label="邮箱">
        <ElInput
          v-model="form.email"
          clearable
          maxlength="200"
          placeholder="请输入邮箱"
          type="email"
        />
      </ElFormItem>
      <ElFormItem label="手机号">
        <ElInput
          v-model="form.phone"
          clearable
          maxlength="50"
          placeholder="请输入手机号"
        />
      </ElFormItem>
      <ElFormItem label="部门">
        <ElSelect
          v-model="form.departmentId"
          clearable
          filterable
          placeholder="选择部门"
          style="width: 100%"
          @change="emit('departmentChange', $event)"
        >
          <ElOption
            v-for="department in departmentOptions"
            :key="department.id"
            :disabled="!department.isActive"
            :label="department.label"
            :value="department.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="直属主管">
        <ElSelect
          v-model="form.supervisorId"
          :loading="supervisorOptionsLoading"
          :remote-method="(keyword: string) => emit('supervisorSearch', keyword)"
          clearable
          filterable
          placeholder="选择直属主管"
          remote
          style="width: 100%"
        >
          <ElOption
            v-for="supervisor in supervisorOptions"
            :key="supervisor.id"
            :label="
              supervisor.employeeNo
                ? `${supervisor.name}（${supervisor.employeeNo}）`
                : supervisor.name
            "
            :value="supervisor.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="角色" required>
        <ElSelect
          v-if="canAssignRole"
          v-model="form.roleId"
          filterable
          placeholder="选择角色"
          style="width: 100%"
        >
          <ElOption
            v-for="role in roles"
            :key="role.id"
            :label="role.name"
            :value="role.id"
          />
        </ElSelect>
        <ElInput v-else :model-value="form.roleName || '未分配'" disabled />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="emit('save')">
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./UserFormDialog.css"></style>
