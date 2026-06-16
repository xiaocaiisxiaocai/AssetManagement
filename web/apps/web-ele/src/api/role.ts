import { requestClient } from '#/api/request';

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
  description?: string | null;
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
  title?: string | null;
  path?: string | null;
  component?: string | null;
  icon?: string | null;
  parentId?: number | null;
  sort: number;
  type: string;
  children?: MenuDto[];
}

export type RolePayload = {
  code?: string;
  name: string;
  description?: string | null;
  isActive?: boolean;
};

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

async function unwrapPaged<T>(request: Promise<ApiResult<PagedResult<T>>>) {
  const result = await request;
  return result.data;
}

export const getRoleListApi = (keyword?: string, page: number = 1, pageSize: number = 20) =>
  unwrapPaged(requestClient.get<ApiResult<PagedResult<RoleDto>>>('/roles', { params: { keyword, page, pageSize } }));

export const createRoleApi = (data: RolePayload) =>
  unwrap(requestClient.post<ApiResult<RoleDto>>('/roles', data));

export const updateRoleApi = (id: number, data: RolePayload) =>
  unwrap(requestClient.put<ApiResult<RoleDto>>(`/roles/${id}`, data));

export const deleteRoleApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/roles/${id}`));

export const setRolePermissionsApi = (id: number, permissionIds: number[]) =>
  unwrap(requestClient.put<ApiResult<null>>(`/roles/${id}/permissions`, { permissionIds }));

export const setRoleMenusApi = (id: number, menuIds: number[]) =>
  unwrap(requestClient.put<ApiResult<null>>(`/roles/${id}/menus`, { menuIds }));

export const getPermissionsApi = () =>
  unwrap(requestClient.get<ApiResult<PermissionDto[]>>('/permissions'));

export const getMenusApi = () =>
  unwrap(requestClient.get<ApiResult<MenuDto[]>>('/menus'));
