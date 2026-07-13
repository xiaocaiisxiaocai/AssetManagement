import type { RouteRecordRaw } from 'vue-router';

import { defineComponent } from 'vue';

import { LOGIN_PATH } from '@vben/constants';

import { AuthPageLayout } from '#/layouts';
import { $t } from '#/locales';
import Login from '#/views/_core/authentication/login.vue';

const EmptyRoot = defineComponent({
  name: 'EmptyRoot',
  render: () => null,
});

/** 全局404页面 */
const fallbackNotFoundRoute: RouteRecordRaw = {
  component: () => import('#/views/_core/fallback/not-found.vue'),
  meta: {
    hideInBreadcrumb: true,
    hideInMenu: true,
    hideInTab: true,
    title: '404',
  },
  name: 'FallbackNotFound',
  path: '/:path(.*)*',
};

/** 基本路由，这些路由是必须存在的 */
const coreRoutes: RouteRecordRaw[] = [
  {
    component: EmptyRoot,
    meta: {
      title: 'Root',
    },
    name: 'Root',
    path: '/',
  },
  {
    component: AuthPageLayout,
    meta: {
      hideInTab: true,
      title: 'Authentication',
    },
    name: 'Authentication',
    path: '/auth',
    redirect: LOGIN_PATH,
    children: [
      {
        name: 'Login',
        path: 'login',
        component: Login,
        meta: {
          title: $t('page.auth.login'),
        },
      },
    ],
  },
];

export { coreRoutes, fallbackNotFoundRoute };
