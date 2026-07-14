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

export type AssetStatus = 0 | 1;

export interface AssetItem {
  assetNo: string;
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
  isFirstRegistration: boolean;
  images?: null | string[];
  locationId?: null | number;
  locationName?: null | string;
  name: string;
  purchaseDate?: null | string;
  quantity: number;
  registrationTime?: null | string;
  remark?: null | string;
  status: AssetStatus;
}

export interface AssetQuery {
  assetNo?: string;
  categoryId?: null | number;
  custodianId?: null | number;
  deleteStatus?: 'active' | 'all' | 'deleted';
  departmentId?: null | number;
  deletedOnly?: boolean;
  name?: string;
  page?: number;
  pageSize?: number;
  status?: AssetStatus | null;
}

export interface AssetPayload {
  categoryId: number;
  custodianId?: null | number;
  departmentId?: null | number;
  images?: string[];
  isFirstRegistration?: boolean;
  locationId?: null | number;
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

export interface ImportPreviewRow {
  categoryCode: string;
  error: string;
  isValid: boolean;
  name: string;
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
  unwrap(
    requestClient.get<ApiResult<PagedResult<AssetItem>>>('/assets', { params }),
  );

export async function getAllAssetsApi(
  params: Omit<AssetQuery, 'page' | 'pageSize'> = {},
): Promise<AssetItem[]> {
  const pageSize = 200;
  const first = await getAssetListApi({ ...params, page: 1, pageSize });
  const pageCount = Math.ceil(first.total / pageSize);
  if (pageCount <= 1) return first.items;
  const remaining = await Promise.all(
    Array.from({ length: pageCount - 1 }, (_, index) =>
      getAssetListApi({ ...params, page: index + 2, pageSize }),
    ),
  );
  return [first, ...remaining].flatMap((page) => page.items);
}

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

export const validateAssetImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<ImportPreviewRow[]>>(
      '/assets/import/validate',
      form,
    ),
  );
};

export const confirmAssetImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<ImportConfirmResult>>(
      '/assets/import/confirm',
      form,
    ),
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

// 图片接口受鉴权保护，使用请求客户端携带 Authorization 获取 Blob，避免 JWT 暴露在 URL、日志和历史记录中。
export async function loadAssetImageObjectUrl(url: string): Promise<string> {
  // requestClient 已以 /api 为 baseURL；持久化地址同样以 /api 开头时需先去掉该前缀，
  // 否则会请求到 /api/api/files/...。
  const requestUrl = url.startsWith('/api/') ? url.slice(4) : url;
  const response = await requestClient.get(requestUrl, {
    responseType: 'blob',
  });
  return URL.createObjectURL(response.data);
}
