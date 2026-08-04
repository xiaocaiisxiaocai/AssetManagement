import { requestClient } from '#/api/request';

import { unwrap } from './unwrap';

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

export type AssetStatus = 0 | 1;

export interface AssetItem {
  assetNo: string;
  canManage: boolean;
  categoryCode: string;
  categoryId: number;
  createdAt: string;
  currentCondition?: null | string;
  deletedAt?: null | string;
  custodianId?: null | number;
  custodianName?: null | string;
  departmentId?: null | number;
  departmentName?: null | string;
  id: number;
  isDeleted: boolean;
  images?: null | string[];
  locationName?: null | string;
  name: string;
  purchaseDate?: null | string;
  quantity: number;
  registrationTime?: null | string;
  remark?: null | string;
  returnDate?: null | string;
  status: AssetStatus;
}

export interface AssetQuery {
  assetNo?: string;
  categoryId?: null | number;
  custodianId?: null | number;
  deleteStatus?: 'active' | 'all' | 'deleted';
  departmentId?: null | number;
  deletedOnly?: boolean;
  excludeCustodianId?: null | number;
  name?: string;
  keyword?: string;
  page?: number;
  pageSize?: number;
  status?: AssetStatus | null;
}

export interface AssetPayload {
  categoryId: number;
  custodianId?: null | number;
  departmentId?: null | number;
  images?: string[];
  locationName?: null | string;
  name: string;
  purchaseDate?: null | string;
  quantity?: number;
  registrationTime?: null | string;
  currentCondition?: null | string;
  remark?: null | string;
  status?: AssetStatus;
}

export interface FileUploadResult {
  name: string;
  url: string;
}

export interface AssetFlow {
  applicant: string;
  applyTime: string;
  bizType: string;
  confirmedAt?: null | string;
  flowNo: string;
  id: number;
  originalReturnDate?: null | string;
  reason?: null | string;
  returnDate?: null | string;
  status: string;
  transferee?: null | string;
  withdrawnAt?: null | string;
}

export interface AssetAuditLog {
  actionType: string;
  id: number;
  occurredAt: string;
  summary: string;
  userId?: null | number;
  userName?: null | string;
}

export interface AssetDetail {
  asset: AssetItem;
  flows: AssetFlow[];
  initialCustodianId?: null | number;
  initialCustodianName?: null | string;
  recentLogs: AssetAuditLog[];
}

export interface AssetImportPreviewRow {
  assetNo?: null | string;
  categoryCode: string;
  custodianEmployeeNo?: null | string;
  custodianId?: null | number;
  custodianName?: null | string;
  currentCondition?: null | string;
  departmentId?: null | number;
  departmentName?: null | string;
  error: string;
  isValid: boolean;
  locationName?: null | string;
  name: string;
  purchaseDate?: null | string;
  quantity: number;
  registrationTime?: null | string;
  remark?: null | string;
  row: number;
}

export interface AssetImportConfirmResult {
  failedCount: number;
  rows: AssetImportPreviewRow[];
  successCount: number;
}

export const getAssetListApi = (params: AssetQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<AssetItem>>>('/assets', { params }),
  );

export const getAssetCategoryCountsApi = () =>
  unwrap(
    requestClient.get<ApiResult<Record<string, number>>>(
      '/assets/category-counts',
    ),
  );

export const getAssetDetailApi = (id: number) =>
  unwrap(requestClient.get<ApiResult<AssetDetail>>(`/assets/${id}/detail`));

export const createAssetApi = (data: AssetPayload) =>
  unwrap(requestClient.post<ApiResult<AssetItem>>('/assets', data));

export const updateAssetApi = (id: number, data: AssetPayload) =>
  unwrap(requestClient.put<ApiResult<AssetItem>>(`/assets/${id}`, data));

export const deleteAssetApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/assets/${id}`));

export const purgeAssetApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/assets/${id}/purge`));

export const restoreAssetApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/assets/${id}/restore`));

export const exportAssetsApi = (params: AssetQuery) =>
  requestClient.get('/assets/export', { params, responseType: 'blob' });

export const downloadAssetImportTemplateApi = () =>
  requestClient.get('/assets/import/template', { responseType: 'blob' });

export const validateAssetImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<AssetImportPreviewRow[]>>(
      '/assets/import/validate',
      form,
    ),
  );
};

export const confirmAssetImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<AssetImportConfirmResult>>(
      '/assets/import/confirm',
      form,
    ),
  );
};

// 上传资产照片,返回 { name, url };url 形如 /api/files/{guid}.ext,可直接用于 <img src>
export const uploadAssetImageApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<FileUploadResult>>('/files/upload', form),
  );
};

const SAFE_ASSET_IMAGE_URL =
  /^\/api\/files\/([0-9a-f]{32}\.(?:gif|jpe?g|png|webp))$/i;

/**
 * 只允许请求本系统生成的图片地址。这个值来自业务数据，不能当作可信 URL。
 */
export function normalizeAssetImageRequestUrl(url: string): string {
  const match = SAFE_ASSET_IMAGE_URL.exec(url);
  if (!match?.[1]) {
    throw new Error('非法的图片地址');
  }
  return `/files/${match[1]}`;
}

// 图片接口受鉴权保护，使用请求客户端携带 Authorization 获取 Blob，避免 JWT 暴露在 URL、日志和历史记录中。
export async function loadAssetImageObjectUrl(url: string): Promise<string> {
  const requestUrl = normalizeAssetImageRequestUrl(url);
  const response = await requestClient.get(requestUrl, {
    responseType: 'blob',
  });
  return URL.createObjectURL(response.data);
}
