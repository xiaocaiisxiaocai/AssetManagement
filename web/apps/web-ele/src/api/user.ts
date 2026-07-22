import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UserDto {
  canManage: boolean;
  id: number;
  employeeNo: string;
  name: string;
  phone?: null | string;
  email?: null | string;
  isActive: boolean;
  departmentId?: null | number;
  departmentName?: null | string;
  roleIds: number[];
  roleNames?: string[];
  supervisorId?: null | number;
  supervisorName?: null | string;
}

export interface UserOptionDto {
  departmentName?: null | string;
  employeeNo: string;
  id: number;
  name: string;
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
  departmentId?: null | number;
  email?: null | string;
  employeeNo?: string;
  name: string;
  phone?: null | string;
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

export const getUserListApi = (
  keyword?: string,
  page: number = 1,
  pageSize: number = 20,
  departmentId?: number,
  roleId?: number,
) =>
  unwrapPaged(
    requestClient.get<ApiResult<PagedResult<UserDto>>>('/users', {
      params: { departmentId, keyword, page, pageSize, roleId },
    }),
  );

/** 业务人员选择器；仅返回活动用户，不要求用户管理权限。 */
export const getUserOptionsPageApi = (keyword = '', page = 1, pageSize = 50) =>
  unwrapPaged(
    requestClient.get<ApiResult<PagedResult<UserOptionDto>>>('/users/options', {
      params: { keyword, page, pageSize },
    }),
  );

/** 保留列表式调用的兼容层；新选择器应使用分页接口做远程搜索。 */
export const getUserOptionsApi = async (keyword = '') => {
  const result = await getUserOptionsPageApi(keyword);
  return result.items;
};

/** 加签人员选择器；仅返回有效部门中的启用部门主管。 */
export const getApproverOptionsApi = (keyword?: string) =>
  unwrap(
    requestClient.get<ApiResult<UserOptionDto[]>>('/users/approver-options', {
      params: { keyword },
    }),
  );

export const createUserApi = (data: UserPayload) =>
  unwrap(requestClient.post<ApiResult<UserDto>>('/users', data));

export const updateUserApi = (id: number, data: UserPayload) =>
  unwrap(requestClient.put<ApiResult<UserDto>>(`/users/${id}`, data));

export const deleteUserApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/users/${id}`));

export const resetUserPasswordApi = (id: number) =>
  unwrap(
    requestClient.post<ApiResult<null>>(`/users/${id}/reset-password`, {}),
  );

export const toggleUserStatusApi = (id: number, isActive: boolean) =>
  unwrap(
    requestClient.post<ApiResult<null>>(`/users/${id}/toggle-status`, {
      isActive,
    }),
  );

export const downloadUserImportTemplateApi = () =>
  requestClient.get('/users/import/template', { responseType: 'blob' });

export const importUsersApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  const config = {
    skipBusinessError: true,
  } as {
    skipBusinessError: boolean;
  } & NonNullable<Parameters<typeof requestClient.post>[2]>;
  return unwrap(
    requestClient.post<ApiResult<UserImportResult>>(
      '/users/import',
      form,
      config,
    ),
  );
};

export const validateUserImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<UserImportResult>>(
      '/users/import/validate',
      form,
    ),
  );
};
