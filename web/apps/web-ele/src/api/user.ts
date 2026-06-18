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

export interface UserDto {
  id: number;
  employeeNo: string;
  name: string;
  email?: string | null;
  phone?: string | null;
  isActive: boolean;
  roleIds: number[];
  roleNames?: string[];
  supervisorId?: null | number;
}

export type UserPayload = {
  employeeNo?: string;
  name: string;
  email?: string | null;
  phone?: string | null;
  roleIds: number[];
  supervisorId?: null | number;
};

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

async function unwrapPaged<T>(request: Promise<ApiResult<PagedResult<T>>>) {
  const result = await request;
  return result.data;
}

export const getUserListApi = (keyword?: string, page: number = 1, pageSize: number = 20) =>
  unwrapPaged(requestClient.get<ApiResult<PagedResult<UserDto>>>('/users', { params: { keyword, page, pageSize } }));

export const createUserApi = (data: UserPayload) =>
  unwrap(requestClient.post<ApiResult<UserDto>>('/users', data));

export const updateUserApi = (id: number, data: UserPayload) =>
  unwrap(requestClient.put<ApiResult<UserDto>>(`/users/${id}`, data));

export const deleteUserApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/users/${id}`));

export const resetUserPasswordApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/users/${id}/reset-password`, {}));

export const toggleUserStatusApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/users/${id}/toggle-status`, {}));
