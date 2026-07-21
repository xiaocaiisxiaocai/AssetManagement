import { createApp, watchEffect } from 'vue';

import { registerAccessDirective } from '@vben/access';
import { preferences } from '@vben/preferences';
import { initStores } from '@vben/stores';
import '@vben/styles';
import '@vben/styles/ele';

import { useTitle } from '@vueuse/core';
import { ElLoading } from 'element-plus';

import { $t, setupI18n } from '#/locales';

import { initComponentAdapter } from './adapter/component';
import App from './app.vue';
import { router } from './router';

// 自定义组件
import { Page } from '@vben/common-ui';

// 引入统一设计系统样式
import './styles/design-system.css';

async function bootstrap(namespace: string) {
  // 初始化组件适配器
  await initComponentAdapter();
  const app = createApp(App);

  // 注册Element Plus提供的v-loading指令
  app.directive('loading', ElLoading.directive);

  // 注册自定义的组件
  app.component('RePage', Page);

  // 国际化 i18n 配置
  await setupI18n(app);

  // 配置 pinia-tore
  await initStores(app, { namespace });

  // 安装权限指令
  registerAccessDirective(app);

  // 配置路由及路由守卫
  app.use(router);

  // 暴露给E2E测试以便进行零刷新路由跳转
  if (import.meta.env.DEV || (window as any).__playwright_active__) {
    (window as any).$router = router;
  }

  // 动态更新标题
  watchEffect(() => {
    if (preferences.app.dynamicTitle) {
      const routeTitle = router.currentRoute.value.meta?.title;
      const pageTitle =
        (routeTitle ? `${$t(routeTitle)} - ` : '') + preferences.app.name;
      useTitle(pageTitle);
    }
  });

  app.mount('#app');
}

export { bootstrap };
