<script lang="ts" setup>
import type { RoleFormState } from './role-form';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElSwitch,
} from 'element-plus';

defineProps<{
  editing: boolean;
  saving: boolean;
}>();

const emit = defineEmits<{
  save: [];
}>();
const form = defineModel<RoleFormState>('form', { required: true });
const visible = defineModel<boolean>('visible', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="editing ? '编辑角色' : '新增角色'"
    width="540px"
  >
    <ElForm class="role-edit-form" label-width="100px">
      <ElFormItem label="角色编码" required>
        <ElInput
          v-model="form.code"
          :disabled="editing"
          maxlength="50"
          placeholder="新增角色时必填"
        />
      </ElFormItem>
      <ElFormItem label="角色名称" required>
        <ElInput
          v-model="form.name"
          maxlength="100"
          placeholder="请输入角色名称"
        />
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

<style scoped src="./RoleFormDialog.css"></style>
