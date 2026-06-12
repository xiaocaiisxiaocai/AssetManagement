<script setup lang="ts">
import { ref } from 'vue';
import type { VxeFormItemProps, VxeFormPropTypes } from 'vxe-pc-ui';
import { changePassword } from "#/api/core/auth";
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
      pattern: '^[a-zA-Z]\\w{5,12}$',
      message: '不能为空，字母开头，长度在6-12位',
    },
  ],
  confirmPassword: [
    {
      required: true,
      pattern: '^[a-zA-Z]\\w{5,12}$',
      message: '不能为空，字母开头，长度在6-12位',
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
const fromItems: VxeFormItemProps<any>[] = [
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
];
const showPopup = ref(false);
const handleSubmit =async () => {
  // TODO: 调用接口修改密码
  const { oldPassword, newPassword } = fromData.value;
  await changePassword({ oldPassword, newPassword });
  showPopup.value = false;
};
const showPasswordPopup = () => {
  showPopup.value = true;
};
defineExpose({ showPasswordPopup });
</script>

<template>
  <div>
    <vxe-modal title="修改密码" v-model="showPopup" :width="500" :height="300">
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
