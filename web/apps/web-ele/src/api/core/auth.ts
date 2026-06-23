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
    token: string;
  }

  /** 组织架构树节点 */
  export interface OrganizationTreeNode {
    id: number;
    name: string;
    children?: OrganizationTreeNode[];
    remark: string;
  }

  /** 角色树节点 */
  export interface RoleTreeNode {
    id: number;
    name: string;
    children?: RoleTreeNode[];
    remark: string;
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
  } as UserInfo & { permissions: string[] };
};

/** 获取组织机构 */
export const getOrganizationTreeData = () => {
  return requestClient.get<AuthApi.OrganizationTreeNode[]>('/auth/organization-tree');
};

/** 获取角色树 */
export const getRoleTreeData = () => {
  return requestClient.get<AuthApi.RoleTreeNode[]>('/auth/role-tree');
};

/**
 * 修改密码
 */
export  function changePassword(data: { oldPassword: string; newPassword: string }) {
  return requestClient.put('/auth/change-password', data);
}
