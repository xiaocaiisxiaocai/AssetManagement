import { useAccessStore } from '@vben/stores';

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
  images?: null | string[];
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
  images?: string[];
  locationId?: null | number;
  model?: null | string;
  name: string;
  price: number;
  quantity?: number;
  status?: AssetStatus;
}

export interface FileUploadResult {
  name: string;
  url: string;
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

export interface AssetFlow {
  applicant: string;
  applyTime: string;
  bizType: string;
  confirmedAt?: null | string;
  flowNo: string;
  id: number;
  reason?: null | string;
  returnDate?: null | string;
  status: string;
  transferee?: null | string;
}

export interface AssetAuditLog {
  actionType: string;
  detail?: null | string;
  id: number;
  ip?: null | string;
  occurredAt: string;
  summary: string;
  targetId?: null | string;
  targetType?: null | string;
  userId?: null | number;
  userName?: null | string;
}

export interface AssetDetail {
  asset: AssetItem;
  flows: AssetFlow[];
  recentLogs: AssetAuditLog[];
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getAssetListApi = (params: AssetQuery) =>
  unwrap(requestClient.get<ApiResult<PagedResult<AssetItem>>>('/assets', { params }));

export const getAssetDetailApi = (id: number) =>
  unwrap(requestClient.get<ApiResult<AssetDetail>>(`/assets/${id}/detail`));

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

// 上传资产照片,返回 { name, url };url 形如 /api/files/{guid}.ext,可直接用于 <img src>
export const uploadAssetImageApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<FileUploadResult>>('/files/upload', form),
  );
};

// 资产图片受 asset:view 鉴权保护,<img>/el-image 无法携带 Authorization 头,
// 经 /api/files 的 query token 通道附加当前 access token。提交前需用 stripImageToken 还原。
export function assetImageUrl(url?: string): string {
  if (!url) {
    return '';
  }
  const token = useAccessStore().accessToken;
  if (!token) {
    return url;
  }
  return `${url}${url.includes('?') ? '&' : '?'}token=${encodeURIComponent(token)}`;
}

// 去除显示用的 token query,得到可持久化的原始 url
export function stripImageToken(url?: string): string {
  return url ? (url.split('?')[0] ?? url) : '';
}
