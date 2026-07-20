import type { UserInfo } from '@vben/types';

import { baseRequestClient, requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export namespace AuthApi {
  /** 登录接口参数 */
  export interface LoginParams {
    account?: string;
    employeeNo?: string;
    password?: string;
  }

  /** 登录接口返回值 */
  export interface LoginResult {
    token: string;
  }
}

/**
 * 登录
 */
export async function loginApi(data: AuthApi.LoginParams) {
  const result = await requestClient.post<ApiResult<AuthApi.LoginResult>>(
    '/auth/login',
    {
      employeeNo: data.employeeNo || data.account,
      password: data.password,
    },
  );
  return result.data;
}

/**
 * 退出登录
 */
export async function logoutApi(token: string) {
  return baseRequestClient.post('/auth/logout', undefined, {
    headers: { Authorization: `Bearer ${token}` },
  });
}

/**
 * 获取用户信息
 * @returns 当前登录用户及其角色、权限信息
 */
export const getUserInfoApi = async () => {
  const result = await requestClient.get<
    ApiResult<{
      employeeNo: string;
      id: number;
      name: string;
      permissions: string[];
      roles: string[];
    }>
  >('/auth/user-info');
  const data = result.data;
  return {
    avatar: '',
    desc: '',
    homePath: '/home',
    realName: data.name,
    roles: data.roles,
    token: '',
    userId: String(data.id),
    username: data.employeeNo,
    permissions: data.permissions,
  } as { permissions: string[] } & UserInfo;
};

/**
 * 修改密码
 */
export function changePassword(data: {
  newPassword: string;
  oldPassword: string;
}) {
  return requestClient.put('/auth/change-password', data);
}
