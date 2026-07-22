<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus';

import { nextTick, reactive, ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
} from 'element-plus';

import { changePassword } from '#/api/core/auth';
import {
  PASSWORD_RULE_MESSAGE,
  PASSWORD_RULE_PATTERN,
} from '#/utils/password-policy';

import {
  closePasswordDialogUnlessSubmitting,
  PASSWORD_DIALOG_WIDTH,
} from './password-dialog';

const emit = defineEmits<{ changed: [] }>();
const showPopup = defineModel<boolean>('open', { default: false });
interface FormDataVO {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}
const formRef = ref<FormInstance>();
const formData = reactive<FormDataVO>({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
});
const formRules: FormRules<FormDataVO> = {
  oldPassword: [{ required: true, message: '旧密码不能为空' }],
  newPassword: [
    {
      required: true,
      pattern: PASSWORD_RULE_PATTERN,
      message: PASSWORD_RULE_MESSAGE,
    },
  ],
  confirmPassword: [
    {
      required: true,
      pattern: PASSWORD_RULE_PATTERN,
      message: PASSWORD_RULE_MESSAGE,
    },
    {
      validator(_rule, value, callback) {
        if (value !== formData.newPassword) {
          callback(new Error('两次输入的密码不一致，请重新输入！'));
          return;
        }
        callback();
      },
      trigger: 'blur',
    },
  ],
};
const submitting = ref(false);
const handleBeforeClose = (done: () => void) =>
  closePasswordDialogUnlessSubmitting(submitting.value, done);
const handleSubmit = async () => {
  if (!formRef.value || submitting.value) return;
  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;

  submitting.value = true;
  try {
    const { oldPassword, newPassword } = formData;
    await changePassword({ oldPassword, newPassword });
    showPopup.value = false;
    ElMessage.success('密码修改成功，请重新登录');
    emit('changed');
  } finally {
    submitting.value = false;
  }
};
watch(
  showPopup,
  (open) => {
    if (!open) return;
    formData.oldPassword = '';
    formData.newPassword = '';
    formData.confirmPassword = '';
    nextTick(() => formRef.value?.clearValidate());
  },
  { immediate: true },
);
</script>

<template>
  <ElDialog
    v-model="showPopup"
    :before-close="handleBeforeClose"
    :close-on-click-modal="false"
    :close-on-press-escape="!submitting"
    :show-close="!submitting"
    :width="PASSWORD_DIALOG_WIDTH"
    title="修改密码"
  >
    <ElForm
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-position="top"
      @submit.prevent="handleSubmit"
    >
      <ElFormItem label="旧密码" prop="oldPassword">
        <ElInput
          v-model="formData.oldPassword"
          autocomplete="current-password"
          placeholder="请输入旧密码"
          show-password
          type="password"
        />
      </ElFormItem>
      <ElFormItem label="新密码" prop="newPassword">
        <ElInput
          v-model="formData.newPassword"
          autocomplete="new-password"
          placeholder="请输入新密码"
          show-password
          type="password"
        />
      </ElFormItem>
      <ElFormItem label="确认密码" prop="confirmPassword">
        <ElInput
          v-model="formData.confirmPassword"
          autocomplete="new-password"
          placeholder="请再次输入新密码"
          show-password
          type="password"
          @keyup.enter="handleSubmit"
        />
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton :disabled="submitting" @click="showPopup = false">
        取消
      </ElButton>
      <ElButton :loading="submitting" type="primary" @click="handleSubmit">
        提交
      </ElButton>
    </template>
  </ElDialog>
</template>
