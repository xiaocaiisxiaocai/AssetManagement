<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { resetAllStores, useUserStore } from '@vben/stores';

import { ElButton, ElForm, ElFormItem, ElInput, ElMessage } from 'element-plus';

import { changePassword } from '#/api/core/auth';
import { useAuthStore } from '#/store';

import {
  formatForceChangePasswordAccount,
  resolveForceChangePasswordTarget,
} from './force-change-password-session';

defineOptions({ name: 'ForceChangePassword' });

const router = useRouter();
const authStore = useAuthStore();
const userStore = useUserStore();
const checking = ref(true);
const loading = ref(false);

const form = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
});

const accountText = computed(() =>
  formatForceChangePasswordAccount(userStore.userInfo),
);

onMounted(async () => {
  try {
    const userInfo = await authStore.fetchUserInfo();
    const target = resolveForceChangePasswordTarget(userInfo);
    if (target) {
      await router.replace(target);
      return;
    }
  } catch {
    ElMessage.warning('登录状态已失效，请重新登录');
    resetAllStores();
    await router.replace(LOGIN_PATH);
  } finally {
    checking.value = false;
  }
});

async function handleSwitchAccount() {
  await authStore.logout(false);
}

async function handleSubmit() {
  if (checking.value) {
    return;
  }
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
        已确认当前账号仍在使用系统默认密码，必须修改后才能进入系统。
      </p>
    </div>

    <div
      class="mb-5 flex items-center justify-between rounded-md border border-gray-200 px-4 py-3 text-sm dark:border-gray-700"
    >
      <div>
        <div class="text-muted-foreground mb-1">当前账号</div>
        <div class="text-foreground font-medium">
          {{ checking ? '正在确认...' : accountText }}
        </div>
      </div>
      <ElButton link type="primary" @click="handleSwitchAccount">
        退出登录 / 切换账号
      </ElButton>
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
          :disabled="checking"
          placeholder="请输入旧密码"
          show-password
          type="password"
        />
      </ElFormItem>
      <ElFormItem label="新密码">
        <ElInput
          v-model="form.newPassword"
          autocomplete="new-password"
          :disabled="checking"
          placeholder="字母开头，长度 6-12 位"
          show-password
          type="password"
        />
      </ElFormItem>
      <ElFormItem label="确认新密码">
        <ElInput
          v-model="form.confirmPassword"
          autocomplete="new-password"
          :disabled="checking"
          placeholder="请再次输入新密码"
          show-password
          type="password"
        />
      </ElFormItem>

      <ElButton
        class="mt-2 w-full"
        :loading="checking || loading"
        type="primary"
        @click="handleSubmit"
      >
        提交修改
      </ElButton>
    </ElForm>
  </div>
</template>
