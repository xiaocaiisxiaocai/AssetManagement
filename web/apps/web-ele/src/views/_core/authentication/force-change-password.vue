<script lang="ts" setup>
import { reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { resetAllStores } from '@vben/stores';

import { ElButton, ElForm, ElFormItem, ElInput, ElMessage } from 'element-plus';

import { changePassword } from '#/api/core/auth';

defineOptions({ name: 'ForceChangePassword' });

const router = useRouter();
const loading = ref(false);

const form = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
});

async function handleSubmit() {
  if (!form.oldPassword) {
    ElMessage.warning('请输入旧密码');
    return;
  }
  if (!/^[a-zA-Z]\w{5,12}$/.test(form.newPassword)) {
    ElMessage.warning('新密码需以字母开头，长度 6-12 位');
    return;
  }
  if (form.newPassword === '123456') {
    ElMessage.warning('新密码不能使用系统默认密码');
    return;
  }
  if (form.newPassword !== form.confirmPassword) {
    ElMessage.warning('两次输入的新密码不一致');
    return;
  }

  loading.value = true;
  try {
    await changePassword({
      oldPassword: form.oldPassword,
      newPassword: form.newPassword,
    });
    ElMessage.success('密码已修改，请使用新密码重新登录');
    resetAllStores();
    await router.replace(LOGIN_PATH);
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="w-full">
    <div class="mb-6">
      <h2 class="text-foreground text-2xl font-semibold">修改初始密码</h2>
      <p class="text-muted-foreground mt-2 text-sm">
        当前账号仍在使用系统默认密码，必须修改后才能进入系统。
      </p>
    </div>

    <ElForm
      :model="form"
      label-position="top"
      @keyup.enter.prevent="handleSubmit"
    >
      <ElFormItem label="旧密码">
        <ElInput
          v-model="form.oldPassword"
          autocomplete="current-password"
          placeholder="请输入旧密码"
          show-password
          type="password"
        />
      </ElFormItem>
      <ElFormItem label="新密码">
        <ElInput
          v-model="form.newPassword"
          autocomplete="new-password"
          placeholder="字母开头，长度 6-12 位"
          show-password
          type="password"
        />
      </ElFormItem>
      <ElFormItem label="确认新密码">
        <ElInput
          v-model="form.confirmPassword"
          autocomplete="new-password"
          placeholder="请再次输入新密码"
          show-password
          type="password"
        />
      </ElFormItem>

      <ElButton
        class="mt-2 w-full"
        :loading="loading"
        type="primary"
        @click="handleSubmit"
      >
        提交修改
      </ElButton>
    </ElForm>
  </div>
</template>
