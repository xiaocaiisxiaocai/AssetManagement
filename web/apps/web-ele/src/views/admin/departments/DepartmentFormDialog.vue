<script lang="ts" setup>
import type {
  DepartmentNode,
  DepartmentPayload,
  OrganizationLevel,
} from '#/api/base-data';
import type { UserOptionDto } from '#/api/user';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
  ElSwitch,
  ElTreeSelect,
} from 'element-plus';

type DepartmentForm = {
  managerId?: number;
} & Omit<DepartmentPayload, 'managerId'>;

defineProps<{
  departments: DepartmentNode[];
  editing: boolean;
  saving: boolean;
  selectableOrganizationLevels: OrganizationLevel[];
  userOptions: UserOptionDto[];
  userOptionsLoading: boolean;
}>();

const emit = defineEmits<{
  parentChange: [];
  save: [];
  userSearch: [keyword: string];
}>();
const form = defineModel<DepartmentForm>('form', { required: true });
const visible = defineModel<boolean>('visible', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="editing ? '编辑组织架构' : '新增组织架构'"
    width="500px"
  >
    <ElForm label-width="100px">
      <ElFormItem label="上级组织">
        <ElTreeSelect
          v-model="form.parentId"
          :data="departments"
          :props="{ children: 'children', label: 'name' }"
          check-strictly
          clearable
          node-key="id"
          placeholder="留空为顶级组织"
          style="width: 100%"
          value-key="id"
          @change="emit('parentChange')"
        />
      </ElFormItem>
      <ElFormItem label="组织架构名称" required>
        <ElInput
          v-model="form.name"
          :maxlength="100"
          placeholder="请输入组织架构名称"
          show-word-limit
        />
      </ElFormItem>
      <ElFormItem label="组织层级" required>
        <ElSelect
          v-model="form.organizationLevelCode"
          placeholder="请选择组织层级"
          style="width: 100%"
        >
          <ElOption
            v-for="level in selectableOrganizationLevels"
            :key="level.code"
            :label="level.name"
            :value="level.code"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="负责人">
        <ElSelect
          v-model="form.managerId"
          :loading="userOptionsLoading"
          :remote-method="(keyword: string) => emit('userSearch', keyword)"
          clearable
          filterable
          placeholder="可选，选择该组织节点负责人"
          remote
          style="width: 100%"
        >
          <ElOption
            v-for="user in userOptions"
            :key="user.id"
            :label="`${user.name}（${user.employeeNo}）`"
            :value="user.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="启用状态">
        <ElSwitch v-model="form.isActive" />
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
