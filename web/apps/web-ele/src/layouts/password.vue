<script setup lang="ts">
import type { VxeFormItemProps, VxeFormPropTypes } from 'vxe-pc-ui';

import { computed, ref } from 'vue';

import { ElMessage } from 'element-plus';

import { changePassword } from '#/api/core/auth';
import {
  PASSWORD_RULE_MESSAGE,
  PASSWORD_RULE_PATTERN,
} from '#/utils/password-policy';

const emit = defineEmits<{ changed: [] }>();
interface FormDataVO {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}
const fromData = ref<FormDataVO>({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
});
const formRules = ref<VxeFormPropTypes.Rules<FormDataVO>>({
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
      validator({ itemValue }) {
        if (itemValue !== fromData.value.newPassword) {
          return new Error('两次输入的密码不一致，请重新输入！');
        }
      },
    },
  ],
});
const fromItems = computed<VxeFormItemProps<any>[]>(() => [
  {
    field: 'oldPassword',
    title: '旧密码',
    span: 24,
    itemRender: {
      name: 'VxeInput',
      props: { type: 'password', placeholder: '请输入旧密码' },
    },
  },
  {
    field: 'newPassword',
    title: '新密码',
    span: 24,
    itemRender: {
      name: 'VxeInput',
      props: { type: 'password', placeholder: '请输入新密码' },
    },
  },
  {
    field: 'confirmPassword',
    title: '确认密码',
    span: 24,
    itemRender: {
      name: 'VxeInput',
      props: { type: 'password', placeholder: '请再次输入新密码' },
    },
  },
  {
    align: 'center',
    span: 24,
    itemRender: {
      name: 'VxeButtonGroup',
      options: [
        { type: 'submit', content: '提交', status: 'primary' },
        { type: 'reset', content: '取消' },
      ],
    },
  },
]);
const showPopup = ref(false);
const handleSubmit = async () => {
  const { oldPassword, newPassword } = fromData.value;
  await changePassword({ oldPassword, newPassword });
  showPopup.value = false;
  ElMessage.success('密码修改成功，请重新登录');
  emit('changed');
};
const showPasswordPopup = () => {
  fromData.value = {
    oldPassword: '',
    newPassword: '',
    confirmPassword: '',
  };
  showPopup.value = true;
};
defineExpose({ showPasswordPopup });
</script>

<template>
  <div>
    <vxe-modal v-model="showPopup" :height="300" :width="500" title="修改密码">
      <template #default>
        <vxe-form
          :data="fromData"
          :items="fromItems"
          :rules="formRules"
          :valid-config="{ theme: 'normal' }"
          title-width="100"
          @reset="showPopup = false"
          @submit="handleSubmit"
        />
      </template>
    </vxe-modal>
  </div>
</template>

<style lang="scss" scoped></style>
