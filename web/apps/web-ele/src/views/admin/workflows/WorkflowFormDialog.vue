<script lang="ts" setup>
import type { SaveWorkflowPayload } from '#/api/workflow';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
} from 'element-plus';

defineProps<{
  bizTypeOptions: { label: string; value: string }[];
  editing: boolean;
  saving: boolean;
}>();

const emit = defineEmits<{
  save: [];
}>();
const form = defineModel<SaveWorkflowPayload>('form', { required: true });
const visible = defineModel<boolean>('visible', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="false"
    :title="editing ? '编辑工作流' : '新增工作流'"
    width="520px"
  >
    <ElForm label-position="top" label-width="90px">
      <ElFormItem label="流程名称" required>
        <ElInput v-model="form.name" placeholder="如：资产借用流程" />
      </ElFormItem>
      <ElFormItem label="业务类型" required>
        <ElSelect
          v-model="form.bizType"
          allow-create
          filterable
          placeholder="请选择业务类型"
          style="width: 100%"
        >
          <ElOption
            v-for="item in bizTypeOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </ElSelect>
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
