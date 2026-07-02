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
  isActive: boolean;
  departmentId?: null | number;
  departmentName?: null | string;
  roleIds: number[];
  roleNames?: string[];
  supervisorId?: null | number;
}

export interface UserImportRow {
  email?: null | string;
  departmentName?: null | string;
  employeeNo: string;
  error: string;
  isValid: boolean;
  name: string;
  roleName: string;
  row: number;
}

export interface UserImportResult {
  failedCount: number;
  rows: UserImportRow[];
  successCount: number;
}

export type UserPayload = {
  employeeNo?: string;
  name: string;
  email?: string | null;
  departmentId?: null | number;
  roleIds: number[];
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

export const toggleUserStatusApi = (id: number, isActive: boolean) =>
  unwrap(requestClient.post<ApiResult<null>>(`/users/${id}/toggle-status`, { isActive }));

export const downloadUserImportTemplateApi = () =>
  requestClient.get('/users/import/template', { responseType: 'blob' });

export const importUsersApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  const config = {
    skipBusinessError: true,
  } as NonNullable<Parameters<typeof requestClient.post>[2]> & {
    skipBusinessError: boolean;
  };
  return unwrap(
    requestClient.post<ApiResult<UserImportResult>>('/users/import', form, config),
  );
};

export const validateUserImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<UserImportResult>>('/users/import/validate', form),
  );
};
