<script lang="ts" setup>
import type { AuditCleanupPreview } from '#/api/report';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElRadioButton,
  ElRadioGroup,
} from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

defineProps<{
  cleanupLoading: boolean;
  preview: AuditCleanupPreview | null;
  previewLoading: boolean;
}>();

const emit = defineEmits<{
  confirm: [];
  preview: [];
}>();
const visible = defineModel<boolean>('visible', { required: true });
const retentionDays = defineModel<number>('retentionDays', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="false"
    title="清理审计日志"
    width="460px"
  >
    <ElForm label-position="top">
      <ElFormItem label="保留周期">
        <ElRadioGroup
          v-model="retentionDays"
          :disabled="cleanupLoading"
          @change="emit('preview')"
        >
          <ElRadioButton :disabled="cleanupLoading" :value="7">
            7 天
          </ElRadioButton>
          <ElRadioButton :disabled="cleanupLoading" :value="14">
            14 天
          </ElRadioButton>
          <ElRadioButton :disabled="cleanupLoading" :value="30">
            30 天
          </ElRadioButton>
        </ElRadioGroup>
      </ElFormItem>

      <div class="cleanup-preview" v-loading="previewLoading">
        <div class="preview-row">
          <span>截止时间</span>
          <strong>{{
            preview
              ? formatDateTime(preview.cutoffTime, { seconds: true })
              : '-'
          }}</strong>
        </div>
        <div class="preview-row">
          <span>预计删除</span>
          <strong>{{ preview?.deleteCount ?? 0 }} 条</strong>
        </div>
        <div class="preview-tip">
          只会删除超过保留周期的审计日志；本次清理动作会单独写入审计日志。
        </div>
      </div>
    </ElForm>

    <template #footer>
      <ElButton :disabled="cleanupLoading" @click="visible = false">
        取消
      </ElButton>
      <ElButton
        :disabled="previewLoading"
        :loading="cleanupLoading"
        type="danger"
        @click="emit('confirm')"
      >
        确认清理
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./AuditCleanupDialog.css"></style>
