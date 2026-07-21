<script lang="ts" setup>
import type { ProjectProgressFormState } from './project-workspace-types';

import type { TestProjectOption } from '#/api/test-project';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
} from 'element-plus';

defineProps<{
  progressOptions: TestProjectOption[];
  saving: boolean;
}>();

const emit = defineEmits<{ save: [] }>();
const form = defineModel<ProjectProgressFormState>('form', { required: true });
const visible = defineModel<boolean>('visible', { default: false });

function onProgressChange(value: string) {
  if (value !== 'closed') form.value.closedDate = '';
}
</script>

<template>
  <ElDialog v-model="visible" title="更新项目进展" width="560px">
    <ElForm label-width="96px">
      <ElFormItem label="进度" required>
        <ElSelect
          v-model="form.progressCode"
          placeholder="请选择项目进度"
          @change="onProgressChange"
        >
          <ElOption
            v-for="item in progressOptions"
            :key="item.id"
            :label="item.label"
            :value="item.code"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem :required="form.progressCode === 'closed'" label="结案时间">
        <ElDatePicker
          v-model="form.closedDate"
          :disabled="form.progressCode !== 'closed'"
          placeholder="选择日期"
          style="width: 100%"
          type="date"
          value-format="YYYY-MM-DD"
        />
      </ElFormItem>
      <ElFormItem label="测试情况">
        <ElInput
          v-model="form.testStatus"
          :rows="5"
          maxlength="1000"
          placeholder="记录当前测试结论、阻塞点或风险"
          show-word-limit
          type="textarea"
        />
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
