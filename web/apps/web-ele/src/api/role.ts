import { requestClient } from '#/api/request';

import { unwrap } from './unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface RoleDto {
  id: number;
  code: string;
  name: string;
  description?: null | string;
  isActive: boolean;
  permissionIds: number[];
  menuIds: number[];
  permissionCount?: number;
  menuCount?: number;
}

export interface PermissionDto {
  id: number;
  code: string;
  name: string;
  module: string;
}

export interface MenuDto {
  id: number;
  name: string;
  title?: null | string;
  path?: null | string;
  component?: null | string;
  icon?: null | string;
  parentId?: null | number;
  permissionCode?: null | string;
  sort: number;
  type: string;
  children?: MenuDto[];
}

export interface RoleAccessOptionsDto {
  menus: MenuDto[];
  permissions: PermissionDto[];
}

export type RolePayload = {
  code?: string;
  description?: null | string;
  isActive?: boolean;
  name: string;
};

export const getRoleListApi = (
  keyword?: string,
  page: number = 1,
  pageSize: number = 20,
) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<RoleDto>>>('/roles', {
      params: { keyword, page, pageSize },
    }),
  );

export const createRoleApi = (data: RolePayload) =>
  unwrap(requestClient.post<ApiResult<RoleDto>>('/roles', data));

export const updateRoleApi = (id: number, data: RolePayload) =>
  unwrap(requestClient.put<ApiResult<RoleDto>>(`/roles/${id}`, data));

export const deleteRoleApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/roles/${id}`));

export const setRolePermissionsApi = (id: number, permissionIds: number[]) =>
  unwrap(
    requestClient.put<ApiResult<null>>(`/roles/${id}/permissions`, {
      permissionIds,
    }),
  );

export const setRoleMenusApi = (id: number, menuIds: number[]) =>
  unwrap(requestClient.put<ApiResult<null>>(`/roles/${id}/menus`, { menuIds }));

export const setRoleAccessApi = (
  id: number,
  permissionIds: number[],
  menuIds: number[],
) =>
  unwrap(
    requestClient.put<ApiResult<RoleDto>>(`/roles/${id}/access`, {
      menuIds,
      permissionIds,
    }),
  );

export const getRoleAccessOptionsApi = () =>
  unwrap(
    requestClient.get<ApiResult<RoleAccessOptionsDto>>('/roles/access-options'),
  );

export const getPermissionsApi = () =>
  unwrap(requestClient.get<ApiResult<PermissionDto[]>>('/permissions'));

export const getMenusApi = () =>
  unwrap(requestClient.get<ApiResult<MenuDto[]>>('/menus'));
