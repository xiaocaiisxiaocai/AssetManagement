import { createMemoryHistory, createRouter } from 'vue-router';

import { describe, expect, it } from 'vitest';

import { generateRoutesByBackend } from '../generate-routes-backend';

describe('generateRoutesByBackend', () => {
  it('菜单请求失败时向上抛出，使路由守卫可以重试', async () => {
    await expect(
      generateRoutesByBackend({
        fetchMenuListAsync: async () => {
          throw new Error('temporary failure');
        },
      }),
    ).rejects.toThrow('temporary failure');
  });

  it('keeps absolute home child routable when home parent avoids root path', async () => {
    const component = () => Promise.resolve({});
    const routes = await generateRoutesByBackend({
      fetchMenuListAsync: async () => [
        {
          name: 'Home',
          path: '/home-root',
          component: 'BasicLayout',
          meta: { title: '首页' },
          children: [
            {
              name: 'HomeWorkspace',
              path: '/home',
              component: '/dashboard/workspace/index',
              meta: { title: '首页' },
            },
          ],
        },
      ],
      layoutMap: { BasicLayout: component },
      pageMap: { '/dashboard/workspace/index.vue': component },
      router: createRouter({
        history: createMemoryHistory(),
        routes: [],
      }),
      routes: [],
    });

    const router = createRouter({
      history: createMemoryHistory(),
      routes: [],
    });
    routes.forEach((route) => router.addRoute(route));

    expect(router.resolve('/home').name).toBe('HomeWorkspace');
  });
});
