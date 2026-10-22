<script lang="ts" setup>
import type { ProjectProgressFormState } from './project-workspace-types';

import type { TestProjectOption } from '#/api/test-project';

import { computed } from 'vue';

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

import { allowedProgressCodes } from './project-workspace-rules';

const props = defineProps<{
  progressBaseline: string;
  progressOptions: TestProjectOption[];
  saving: boolean;
}>();

const emit = defineEmits<{ save: [] }>();
const form = defineModel<ProjectProgressFormState>('form', { required: true });
const visible = defineModel<boolean>('visible', { default: false });

const selectableProgressOptions = computed(() => {
  const allowed = new Set(allowedProgressCodes(props.progressBaseline));
  return props.progressOptions.filter((item) =>
    allowed.has(item.code.toLowerCase()),
  );
});

function onProgressChange(value: string) {
  if (value !== 'closed') form.value.closedDate = '';
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="!saving"
    :close-on-press-escape="!saving"
    :show-close="!saving"
    title="更新项目进展"
    width="560px"
  >
    <ElForm label-width="96px">
      <ElFormItem label="进度" required>
        <ElSelect
          v-model="form.progressCode"
          placeholder="请选择项目进度"
          @change="onProgressChange"
        >
          <ElOption
            v-for="item in selectableProgressOptions"
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
      <ElFormItem label="项目摘要">
        <ElInput
          v-model="form.testStatus"
          :rows="5"
          maxlength="1000"
          placeholder="记录项目级进展、风险和阻塞点，不填写阶段测试结果"
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
