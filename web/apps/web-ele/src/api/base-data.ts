import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export interface TreeNodeBase {
  children: TreeNodeBase[];
  id: number;
  name: string;
  parentId?: null | number;
}

export interface DepartmentNode extends TreeNodeBase {
  assetCount: number;
  code: string;
  isActive: boolean;
  managerName?: null | string;
  children: DepartmentNode[];
}

export interface CategoryNode extends TreeNodeBase {
  code: string;
  codeSeg: string;
  children: CategoryNode[];
}

export interface LocationNode extends TreeNodeBase {
  qrCode?: null | string;
  children: LocationNode[];
}

export interface SystemSetting {
  description?: null | string;
  id: number;
  key: string;
  value: string;
}

export type DepartmentPayload = {
  code: string;
  isActive?: boolean;
  managerId?: null | number;
  name: string;
  parentId?: null | number;
};

export type CategoryPayload = {
  codeSeg: string;
  name: string;
  parentId?: null | number;
};

export type LocationPayload = {
  name: string;
  parentId?: null | number;
  qrCode?: null | string;
};

export type SettingPayload = {
  description?: null | string;
  key: string;
  value: string;
};

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getDepartmentTreeApi = () =>
  unwrap(requestClient.get<ApiResult<DepartmentNode[]>>('/departments/tree'));

export const createDepartmentApi = (data: DepartmentPayload) =>
  unwrap(requestClient.post<ApiResult<DepartmentNode>>('/departments', data));

export const updateDepartmentApi = (id: number, data: DepartmentPayload) =>
  unwrap(requestClient.put<ApiResult<DepartmentNode>>(`/departments/${id}`, data));

export const deleteDepartmentApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/departments/${id}`));

export const getCategoryTreeApi = () =>
  unwrap(requestClient.get<ApiResult<CategoryNode[]>>('/categories/tree'));

export const createCategoryApi = (data: CategoryPayload) =>
  unwrap(requestClient.post<ApiResult<CategoryNode>>('/categories', data));

export const updateCategoryApi = (id: number, data: CategoryPayload) =>
  unwrap(requestClient.put<ApiResult<CategoryNode>>(`/categories/${id}`, data));

export const deleteCategoryApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/categories/${id}`));

export const getLocationTreeApi = () =>
  unwrap(requestClient.get<ApiResult<LocationNode[]>>('/locations/tree'));

export const createLocationApi = (data: LocationPayload) =>
  unwrap(requestClient.post<ApiResult<LocationNode>>('/locations', data));

export const updateLocationApi = (id: number, data: LocationPayload) =>
  unwrap(requestClient.put<ApiResult<LocationNode>>(`/locations/${id}`, data));

export const deleteLocationApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/locations/${id}`));

export const getSettingsApi = () =>
  unwrap(requestClient.get<ApiResult<SystemSetting[]>>('/settings'));

export const saveSettingsApi = (data: SettingPayload[]) =>
  unwrap(requestClient.put<ApiResult<SystemSetting[]>>('/settings', data));
