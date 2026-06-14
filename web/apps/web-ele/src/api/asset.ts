import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export type AssetStatus = 0 | 1 | 2 | 3;

export interface AssetItem {
  assetNo: string;
  brand?: null | string;
  categoryCode: string;
  categoryId: number;
  categoryName: string;
  createdAt: string;
  custodianId?: null | number;
  custodianName?: null | string;
  departmentId?: null | number;
  departmentName?: null | string;
  id: number;
  locationId?: null | number;
  locationName?: null | string;
  model?: null | string;
  name: string;
  price: number;
  quantity: number;
  status: AssetStatus;
}

export interface AssetQuery {
  assetNo?: string;
  categoryId?: null | number;
  departmentId?: null | number;
  name?: string;
  page?: number;
  pageSize?: number;
  status?: AssetStatus | null;
}

export interface AssetPayload {
  brand?: null | string;
  categoryId: number;
  custodianId?: null | number;
  departmentId?: null | number;
  locationId?: null | number;
  model?: null | string;
  name: string;
  price: number;
  quantity?: number;
  status?: AssetStatus;
}

export interface ImportPreviewRow {
  brand?: null | string;
  categoryCode: string;
  error: string;
  isValid: boolean;
  model?: null | string;
  name: string;
  price: number;
  row: number;
}

export interface ImportConfirmResult {
  failedCount: number;
  rows: ImportPreviewRow[];
  successCount: number;
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getAssetListApi = (params: AssetQuery) =>
  unwrap(requestClient.get<ApiResult<PagedResult<AssetItem>>>('/assets', { params }));

export const createAssetApi = (data: AssetPayload) =>
  unwrap(requestClient.post<ApiResult<AssetItem>>('/assets', data));

export const updateAssetApi = (id: number, data: AssetPayload) =>
  unwrap(requestClient.put<ApiResult<AssetItem>>(`/assets/${id}`, data));

export const deleteAssetApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/assets/${id}`));

export const validateAssetImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<ImportPreviewRow[]>>('/assets/import/validate', form),
  );
};

export const confirmAssetImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<ImportConfirmResult>>('/assets/import/confirm', form),
  );
};

export const downloadAssetTemplateApi = () =>
  requestClient.get('/assets/import/template', { responseType: 'blob' });

export const exportAssetsApi = (params: AssetQuery) =>
  requestClient.get('/assets/export', { params, responseType: 'blob' });
