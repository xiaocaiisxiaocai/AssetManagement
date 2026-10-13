<script lang="ts" setup>
import type { VbenFormSchema } from '@vben/common-ui';

import { computed, onMounted, ref, watch } from 'vue';

import { AuthenticationLogin, z } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { useAccessStore } from '@vben/stores';

import Password from '#/layouts/password.vue';
import { useAuthStore } from '#/store';
import { tokenRequiresPasswordChange } from '#/utils/must-change-password';

defineOptions({ name: 'Login' });

const authStore = useAuthStore();
const accessStore = useAccessStore();
const mustChangePassword = ref(false);

// 弹窗状态始终跟随令牌：令牌被清除（401 登出、改密后退出）时一并关闭，避免卡在无法关闭的弹窗。
function syncForcedPasswordDialog() {
  mustChangePassword.value = tokenRequiresPasswordChange(
    accessStore.accessToken,
  );
}

onMounted(syncForcedPasswordDialog);
watch(() => accessStore.accessToken, syncForcedPasswordDialog);

const formSchema = computed((): VbenFormSchema[] => {
  return [
    {
      component: 'VbenInput',
      componentProps: {
        autocomplete: 'username',
        placeholder: $t('authentication.usernameTip'),
      },
      fieldName: 'account',
      label: $t('authentication.username'),
      rules: z.string().min(1, { message: $t('authentication.usernameTip') }),
    },
    {
      component: 'VbenInputPassword',
      componentProps: {
        autocomplete: 'current-password',
        placeholder: $t('authentication.password'),
      },
      fieldName: 'password',
      label: $t('authentication.password'),
      rules: z.string().min(1, { message: $t('authentication.passwordTip') }),
    },
  ];
});

async function handleLogin(values: any) {
  try {
    const result = await authStore.authLogin(values);
    mustChangePassword.value = Boolean(result?.mustChangePassword);
  } catch {
    // 错误提示已由请求拦截器(request.ts)统一弹出
  }
}

async function leaveForcedPasswordChange() {
  mustChangePassword.value = false;
  await authStore.logout(false);
}
</script>

<template>
  <div>
    <AuthenticationLogin
      :form-schema="formSchema"
      :loading="authStore.loginLoading"
      :show-code-login="false"
      :show-qrcode-login="false"
      :show-register="false"
      :show-remember-me="false"
      :show-third-party-login="false"
      title="资产管理系统"
      @submit="handleLogin"
    />
    <Password
      v-model:open="mustChangePassword"
      forced
      @changed="leaveForcedPasswordChange"
      @logout="leaveForcedPasswordChange"
    />
  </div>
</template>
