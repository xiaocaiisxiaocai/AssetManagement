import { baseRequestClient, requestClient } from '#/api/request';
import type { UserInfo } from '@vben/types';

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
    mustChangePassword: boolean;
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
export async function logoutApi() {
  return baseRequestClient.post('/auth/logout', {
    withCredentials: true,
  });
}

/**
 * 获取用户权限码
 */
export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/auth/functions');
}

/**
 * 获取用户信息
 * @returns
 */
export const getUserInfoApi = async () => {
  const result = await requestClient.get<
    ApiResult<{
      employeeNo: string;
      id: number;
      mustChangePassword: boolean;
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
    mustChangePassword: data.mustChangePassword,
    permissions: data.permissions,
  } as UserInfo & { permissions: string[] };
};

/**
 * 修改密码
 */
export function changePassword(data: { oldPassword: string; newPassword: string }) {
  return requestClient.put('/auth/change-password', data);
}
