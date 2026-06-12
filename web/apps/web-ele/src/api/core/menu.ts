import type { RouteRecordStringComponent } from '@vben/types';

import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

/**
 * 获取用户所有菜单
 */
export async function getAllMenusApi() {
  const result =
    await requestClient.get<ApiResult<RouteRecordStringComponent[]>>(
      '/menu/routes',
    );
  return result.data;
}
