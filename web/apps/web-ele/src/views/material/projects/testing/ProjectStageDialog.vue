<script lang="ts" setup>
import type { ProjectStageFormState } from './project-testing-rules';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElOption,
  ElSelect,
} from 'element-plus';

import { businessDateText } from '#/utils/business-date';

import { localToday } from './project-testing-rules';

defineProps<{
  editing: boolean;
  saving: boolean;
}>();

const emit = defineEmits<{ save: [] }>();
const visible = defineModel<boolean>('visible', { required: true });
const form = defineModel<ProjectStageFormState>('form', { required: true });

function disableFutureEnd(date: Date) {
  return (
    form.value.status === 'completed' && localToday(date) > businessDateText()
  );
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="!saving"
    :close-on-press-escape="!saving"
    :show-close="!saving"
    :title="editing ? '编辑测试阶段' : '新增测试阶段'"
    append-to-body
    class="project-stage-dialog"
    destroy-on-close
    width="min(900px, calc(100vw - 32px))"
  >
    <ElForm label-position="top">
      <div class="stage-form-grid">
        <ElFormItem label="阶段序号" required>
          <ElInputNumber
            v-model="form.sequence"
            :max="999"
            :min="1"
            :precision="0"
            controls-position="right"
            placeholder="1-999"
            style="width: 100%"
          />
        </ElFormItem>
        <ElFormItem label="阶段名称" required>
          <ElInput
            v-model="form.name"
            :maxlength="100"
            placeholder="请输入阶段名称"
            show-word-limit
          />
        </ElFormItem>
        <ElFormItem label="阶段状态" required>
          <ElSelect v-model="form.status" placeholder="请选择状态">
            <ElOption label="计划中" value="planned" />
            <ElOption label="进行中" value="in_progress" />
            <ElOption label="已完成" value="completed" />
            <ElOption label="受阻" value="blocked" />
          </ElSelect>
        </ElFormItem>
        <div class="stage-date-grid">
          <ElFormItem
            :required="
              form.status === 'in_progress' ||
              form.status === 'blocked' ||
              form.status === 'completed'
            "
            label="开始日期"
          >
            <ElDatePicker
              v-model="form.startDate"
              placeholder="选择日期"
              style="width: 100%"
              type="date"
              value-format="YYYY-MM-DD"
            />
          </ElFormItem>
          <ElFormItem :required="form.status === 'completed'" label="结束日期">
            <ElDatePicker
              v-model="form.endDate"
              :disabled-date="disableFutureEnd"
              placeholder="选择日期"
              style="width: 100%"
              type="date"
              value-format="YYYY-MM-DD"
            />
          </ElFormItem>
        </div>
      </div>
      <ElFormItem label="阶段目标">
        <ElInput
          v-model="form.objective"
          :maxlength="2000"
          :rows="3"
          placeholder="填写本阶段目标和验收重点"
          resize="vertical"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
      <ElFormItem :required="form.status === 'completed'" label="测试内容">
        <ElInput
          v-model="form.testContent"
          :maxlength="8000"
          :rows="5"
          placeholder="填写测试范围、方法、过程和关键数据"
          resize="vertical"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
      <ElFormItem :required="form.status === 'completed'" label="测试结果">
        <ElInput
          v-model="form.testResult"
          :maxlength="8000"
          :rows="5"
          placeholder="填写测试结果、异常和处置情况"
          resize="vertical"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton :disabled="saving" @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="emit('save')">
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./ProjectStageDialog.css"></style>
