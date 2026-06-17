<script lang="ts" setup>
import type { VbenFormSchema } from '@vben/common-ui';

import { computed } from 'vue';

import { AuthenticationLogin, z } from '@vben/common-ui';
import { $t } from '@vben/locales';

import { useAuthStore } from '#/store';

defineOptions({ name: 'Login' });

const authStore = useAuthStore();

const formSchema = computed((): VbenFormSchema[] => {
  return [
    {
      component: 'VbenInput',
      componentProps: {
        placeholder: $t('authentication.usernameTip'),
      },
      fieldName: 'account',
      defaultValue: '1001',
      label: $t('authentication.username'),
      rules: z.string().min(1, { message: $t('authentication.usernameTip') }),
    },
    {
      component: 'VbenInputPassword',
      componentProps: {
        placeholder: $t('authentication.password'),
      },
      fieldName: 'password',
      label: $t('authentication.password'),
      defaultValue: '123456',
      rules: z.string().min(1, { message: $t('authentication.passwordTip') }),
    },
  ];
});

async function handleLogin(values: any) {
  try {
    await authStore.authLogin(values);
  } catch {
    // 登录失败的错误提示已由请求拦截器(request.ts)统一弹出,此处无需重复弹框
  }
}
</script>

<template>
  <AuthenticationLogin
    :form-schema="formSchema"
    :loading="authStore.loginLoading"
    title="资产管理系统"
    :show-code-login="false"
    :show-qrcode-login="false"
    :show-third-party-login="false"
    :show-register="false"
    @submit="handleLogin"
  />
</template>
