<script setup lang="ts">
import { computed, ref } from 'vue';
import type { VxeFormItemProps, VxeFormPropTypes } from 'vxe-pc-ui';
import { ElMessage } from 'element-plus';
import { changePassword } from '#/api/core/auth';

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
      pattern: '^(?=.*[A-Za-z])(?=.*\\d).{8,128}$',
      message: '请输入 8-128 位且同时包含字母和数字的密码',
    },
  ],
  confirmPassword: [
    {
      required: true,
      pattern: '^(?=.*[A-Za-z])(?=.*\\d).{8,128}$',
      message: '请输入 8-128 位且同时包含字母和数字的密码',
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
const requiredChange = ref(false);
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
        ...(requiredChange.value ? [] : [{ type: 'reset', content: '取消' }]),
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
const showPasswordPopup = (required = false) => {
  fromData.value = {
    oldPassword: '',
    newPassword: '',
    confirmPassword: '',
  };
  requiredChange.value = required;
  showPopup.value = true;
};
defineExpose({ showPasswordPopup });
</script>

<template>
  <div>
    <vxe-modal
      v-model="showPopup"
      :esc-closable="!requiredChange"
      :height="300"
      :mask-closable="!requiredChange"
      :show-close="!requiredChange"
      :title="requiredChange ? '首次登录，请修改密码' : '修改密码'"
      :width="500"
    >
      <template #default>
        <vxe-form
          :data="fromData"
          :items="fromItems"
          :rules="formRules"
          :valid-config="{ theme: 'normal' }"
          title-width="100"
          @reset="showPopup = false"
          @submit="handleSubmit"
        >
        </vxe-form>
      </template>
    </vxe-modal>
  </div>
</template>

<style lang="scss" scoped></style>
