import type { RouteRecordRaw } from 'vue-router';

import { mergeRouteModules, traverseTreeValues } from '@vben/utils';

import { BasicLayout } from '#/layouts';

import { coreRoutes, fallbackNotFoundRoute } from './core';

const dynamicRouteFiles = import.meta.glob('./modules/**/*.ts', {
  eager: true,
});

// 有需要可以自行打开注释，并创建文件夹
// const externalRouteFiles = import.meta.glob('./external/**/*.ts', { eager: true });
// const staticRouteFiles = import.meta.glob('./static/**/*.ts', { eager: true });

/** 动态路由 */
const dynamicRoutes: RouteRecordRaw[] = mergeRouteModules(dynamicRouteFiles);

/** 外部路由列表，访问这些页面可以不需要Layout，可能用于内嵌在别的系统(不会显示在菜单中) */
// const externalRoutes: RouteRecordRaw[] = mergeRouteModules(externalRouteFiles);
// const staticRoutes: RouteRecordRaw[] = mergeRouteModules(staticRouteFiles);
const staticRoutes: RouteRecordRaw[] = [];
const externalRoutes: RouteRecordRaw[] = [];

// 后端菜单按单一权限码授权，无法表达“资产审批或料件审批”这一类复合入口。
// 这两个隐藏路由由前端守卫校验与页面 API 一致的料件权限码。
const hiddenAuthenticatedRoutes: RouteRecordRaw[] = [
  {
    component: BasicLayout,
    meta: { hideInMenu: true, title: '料件流程' },
    name: 'MaterialFlowGlobal',
    path: '/material-flow-global',
    children: [
      {
        component: () => import('#/views/approval/pending/index.vue'),
        meta: {
          hideInMenu: true,
          requiredAccessCodes: ['material-flow:approve'],
          title: '料件待我审批',
        },
        name: 'MaterialFlowPendingGlobal',
        path: '/material/approvals',
      },
      {
        component: () => import('#/views/approval/mine/index.vue'),
        meta: {
          hideInMenu: true,
          requiredAccessCodes: ['material-flow:view'],
          title: '我的料件申请',
        },
        name: 'MaterialFlowMineGlobal',
        path: '/material/applications',
      },
    ],
  },
  {
    component: () => import('#/views/_core/fallback/forbidden.vue'),
    meta: {
      hideInBreadcrumb: true,
      hideInMenu: true,
      hideInTab: true,
      title: '403',
    },
    name: 'FallbackForbidden',
    path: '/403',
  },
];

/** 路由列表，由基本路由、外部路由和404兜底路由组成
 *  无需走权限验证（会一直显示在菜单中） */
const routes: RouteRecordRaw[] = [
  ...coreRoutes,
  ...hiddenAuthenticatedRoutes,
  ...externalRoutes,
];

/** 基本路由列表，这些路由不需要进入权限拦截 */
// 根路径需要经过权限守卫，先装载动态路由后再跳转首页；否则首次访问时
// Vue Router 会尝试解析尚未注册的 /home，并在控制台留下导航异常。
const coreRouteNames = traverseTreeValues(
  coreRoutes,
  (route) => route.name,
).filter((name) => name !== 'Root');

/** 有权限校验的路由列表，包含动态路由和静态路由 */
const accessRoutes = [...dynamicRoutes, ...staticRoutes];
export { accessRoutes, coreRouteNames, fallbackNotFoundRoute, routes };
