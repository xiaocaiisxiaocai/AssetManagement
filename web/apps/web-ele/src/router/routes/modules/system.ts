import type { RouteRecordRaw } from 'vue-router';

import { BasicLayout } from '#/layouts';
import { $t } from '#/locales';

const routes: RouteRecordRaw[] = [
  {
    component: BasicLayout,
    meta: {
      icon: 'tdesign:system-setting-filled',
      keepAlive: true,
      order: 900,
      authority: ['system'],
      title: $t('page.system.title'),
    },
    name: 'System',
    path: '/system',
    children: [
      {
        meta: {
          title: $t('page.system.user.title'),
          authority: ['system.user'],
          icon: `tdesign:user-list`,
        },
        name: 'User',
        path: '/system/user',
        component: () => import('#/views/system/user/index.vue'),
      },
      {
        meta: {
          title: $t('page.system.role.title'),
          authority: ['system.role'],
          icon: `tdesign:usergroup`,
        },
        name: 'Role',
        path: '/system/role',
        component: () => import('#/views/system/role/index.vue'),
      },
      {
        meta: {
          title: $t('page.system.function.title'),
          authority: ['system.function'],
          icon: `tdesign:application`,
        },
        name: 'Function',
        path: '/system/function',
        component: () => import('#/views/system/function/index.vue'),
      },
      {
        meta: {
          title: $t('page.system.organization.title'),
          authority: ['system.organization'],
          icon: `tdesign:tree-list`,
        },
        name: 'Organization',
        path: '/system/organization',
        component: () => import('#/views/system/organization/index.vue'),
      },
      {
        meta: {
          title: $t('page.system.dict.title'),
          authority: ['system.dictionary'],
          icon: `tdesign:book`,
        },
        name: 'Dict',
        path: '/system/dict',
        component: () => import('#/views/system/dictionary/index.vue'),
      },
      {
        meta: {
          title: $t('page.system.config.title'),
          authority: ['system.systemconfig'],
          icon: `tdesign:file-setting`,
        },
        name: 'Config',
        path: '/system/config',
        component: () => import('#/views/system/systemConfig/index.vue'),
      },
      {
        meta: {
          title: $t('page.system.onlineUser.title'),
          authority: ['system.onlineuser'],
          icon: 'tdesign:user-talk',
        },
        name: 'OnlineUser',
        path: '/system/online-user',
        component: () => import('#/views/system/onlineUser/index.vue'),
      },
    ],
  },
];

export default routes;
