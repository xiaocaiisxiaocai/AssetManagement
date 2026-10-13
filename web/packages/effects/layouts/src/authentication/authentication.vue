<script setup lang="ts">
import type { ToolbarType } from './types';

import { preferences, usePreferences } from '@vben/preferences';

import { Copyright } from '../basic/copyright';
import AuthenticationFormView from './form.vue';
import SloganIcon from './icons/slogan.vue';
import Toolbar from './toolbar.vue';

interface Props {
  appName?: string;
  logo?: string;
  pageTitle?: string;
  pageDescription?: string;
  sloganImage?: string;
  toolbar?: boolean;
  copyright?: boolean;
  toolbarList?: ToolbarType[];
}

withDefaults(defineProps<Props>(), {
  appName: '',
  copyright: true,
  logo: '',
  pageDescription: '',
  pageTitle: '',
  sloganImage: '',
  toolbar: true,
  toolbarList: () => ['color', 'language', 'layout', 'theme'],
});

const { authPanelCenter, authPanelLeft, authPanelRight, isDark } =
  usePreferences();
</script>

<template>
  <div
    :class="[isDark]"
    class="h-dvh w-full select-none overflow-x-hidden overflow-y-auto"
  >
    <div class="flex min-h-full w-full">
    <template v-if="toolbar">
      <slot name="toolbar">
        <Toolbar :toolbar-list="toolbarList" />
      </slot>
    </template>
    <!-- 左侧认证面板 -->
    <AuthenticationFormView
      v-if="authPanelLeft"
      class="min-h-full w-full lg:w-[clamp(30rem,42vw,36rem)] lg:flex-none"
      transition-name="slide-left"
    >
      <template v-if="copyright" #copyright>
        <slot name="copyright">
          <Copyright
            v-if="preferences.copyright.enable"
            v-bind="preferences.copyright"
          />
        </slot>
      </template>
    </AuthenticationFormView>

    <!-- 头部 Logo 和应用名称 -->
    <div v-if="logo || appName" class="fixed left-0 top-0 z-10 flex">
      <div
        class="text-foreground lg:text-foreground ml-4 mt-4 flex flex-1 items-center sm:left-6 sm:top-6"
      >
        <img v-if="logo" :alt="appName" :src="logo" class="mr-2" width="42" />
        <p v-if="appName" class="text-xl font-medium">
          {{ appName }}
        </p>
      </div>
    </div>

    <!-- 系统介绍 -->
    <div
      v-if="!authPanelCenter"
      class="relative hidden min-h-full w-0 min-w-0 flex-1 lg:flex"
    >
      <div
        class="bg-background-deep relative flex w-full items-center justify-center px-8 py-16 dark:bg-[#070709]"
      >
        <div class="login-background pointer-events-none absolute inset-0"></div>
        <div class="relative flex w-full max-w-xl flex-col items-center">
          <template v-if="sloganImage">
            <img
              :alt="appName"
              :src="sloganImage"
              class="animate-float h-auto max-h-[min(16rem,38vh)] w-[min(70%,18rem)]"
            />
          </template>
          <SloganIcon
            v-else
            :alt="appName"
            class="animate-float h-auto max-h-[min(16rem,38vh)] w-[min(70%,18rem)]"
          />
          <div
            class="text-1xl text-foreground mt-6 text-center font-sans lg:text-2xl"
          >
            {{ pageTitle }}
          </div>
          <div class="dark:text-muted-foreground mt-2 text-center">
            {{ pageDescription }}
          </div>
        </div>
      </div>
    </div>

    <!-- 中心认证面板 -->
    <div v-if="authPanelCenter" class="flex-center relative min-h-full w-full">
      <div class="login-background absolute left-0 top-0 size-full"></div>
      <AuthenticationFormView
        class="md:bg-background shadow-primary/5 shadow-float w-full rounded-3xl pb-20 md:w-2/3 lg:w-1/2 xl:w-[36%]"
      >
        <template v-if="copyright" #copyright>
          <slot name="copyright">
            <Copyright
              v-if="preferences.copyright.enable"
              v-bind="preferences.copyright"
            />
          </slot>
        </template>
      </AuthenticationFormView>
    </div>

    <!-- 右侧认证面板 -->
    <AuthenticationFormView
      v-if="authPanelRight"
      class="min-h-full w-full lg:w-[clamp(30rem,42vw,36rem)] lg:flex-none"
    >
      <template v-if="copyright" #copyright>
        <slot name="copyright">
          <Copyright
            v-if="preferences.copyright.enable"
            v-bind="preferences.copyright"
          />
        </slot>
      </template>
    </AuthenticationFormView>
    </div>
  </div>
</template>

<style scoped>
.login-background {
  background: linear-gradient(
    154deg,
    #07070915 30%,
    hsl(var(--primary) / 30%) 48%,
    #07070915 64%
  );
  filter: blur(100px);
}

.dark {
  .login-background {
    background: linear-gradient(
      154deg,
      #07070915 30%,
      hsl(var(--primary) / 20%) 48%,
      #07070915 64%
    );
    filter: blur(100px);
  }
}
</style>
