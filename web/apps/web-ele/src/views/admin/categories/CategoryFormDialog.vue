<script lang="ts" setup>
import type { CategoryPayload } from '#/api/base-data';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElTag,
} from 'element-plus';

defineProps<{
  codeRuleHint: string;
  editing: boolean;
  isRootCategory: boolean;
  parentDisplay: string;
  previewCode: string;
  saving: boolean;
}>();

const emit = defineEmits<{
  save: [];
}>();

const form = defineModel<CategoryPayload>('form', { required: true });
const visible = defineModel<boolean>('visible', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="editing ? '编辑分类' : '新增分类'"
    width="500px"
  >
    <ElForm label-width="100px" @submit.prevent="emit('save')">
      <ElFormItem label="上级分类">
        <ElTag size="default">{{ parentDisplay }}</ElTag>
      </ElFormItem>
      <ElFormItem label="编码段" required>
        <ElInput v-model="form.codeSeg" placeholder="请输入编码段" />
        <div class="form-tip">{{ codeRuleHint }}</div>
      </ElFormItem>
      <ElFormItem v-if="!isRootCategory" label="备注">
        <ElInput
          v-model="form.remark"
          :rows="3"
          placeholder="请输入备注信息"
          type="textarea"
        />
      </ElFormItem>
      <ElFormItem label="完整编码">
        <ElTag size="default" type="info">
          {{ previewCode || '待输入编码段' }}
        </ElTag>
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

<style scoped src="./CategoryFormDialog.css"></style>
