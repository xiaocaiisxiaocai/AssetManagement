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

export interface CategoryStatRow {
  available: number;
  borrowed: number;
  categoryCode: string;
  categoryId: number;
  percent: number;
  total: number;
}

export interface DeptStatRow {
  available: number;
  borrowed: number;
  departmentId: number;
  departmentName: string;
  percent: number;
  total: number;
}

export interface AssetSummary {
  available: number;
  borrowed: number;
  byCategory: CategoryStatRow[];
  byDept: DeptStatRow[];
  total: number;
}

export interface BorrowReportQuery {
  borrowerId?: number;
  categoryId?: number;
  endTime?: string;
  page?: number;
  pageSize?: number;
  startTime?: string;
  status?: string;
}

export interface BorrowReportRow {
  applyTime: string;
  assetId: number;
  assetName: string;
  assetNo: string;
  borrower: string;
  borrowerDept?: null | string;
  borrowerId: number;
  categoryCode: string;
  flowId: number;
  flowNo: string;
  returnDate?: null | string;
  status: string;
}

export interface OverdueReportRow {
  assetId: number;
  assetName: string;
  assetNo: string;
  borrower: string;
  borrowerDept?: null | string;
  borrowerId: number;
  categoryCode: string;
  flowId: number;
  isSerious: boolean;
  overdueDays: number;
  returnDate: string;
}

export interface AuditLogQuery {
  actionType?: string;
  endTime?: string;
  module?: string;
  page?: number;
  pageSize?: number;
  startTime?: string;
  userId?: number;
}

export interface AuditLogRow {
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

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getAssetSummaryApi = () =>
  unwrap(requestClient.get<ApiResult<AssetSummary>>('/reports/summary'));

export const exportAssetSummaryApi = () =>
  requestClient.get('/reports/summary/export', { responseType: 'blob' });

export const getBorrowReportApi = (params: BorrowReportQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<BorrowReportRow>>>('/reports/borrowed', {
      params,
    }),
  );

export const exportBorrowReportApi = (params: BorrowReportQuery) =>
  requestClient.get('/reports/borrowed/export', { params, responseType: 'blob' });

export const getOverdueReportApi = () =>
  unwrap(requestClient.get<ApiResult<OverdueReportRow[]>>('/reports/overdue'));

export const exportOverdueReportApi = () =>
  requestClient.get('/reports/overdue/export', { responseType: 'blob' });

export const remindOverdueApi = (assetId: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/reports/overdue/${assetId}/remind`, {}));

export const remindOverdueBatchApi = (assetIds: number[]) =>
  unwrap(
    requestClient.post<ApiResult<null>>('/reports/overdue/remind-batch', {
      assetIds,
    }),
  );

export const getAuditLogsApi = (params: AuditLogQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<AuditLogRow>>>('/audit-logs', {
      params,
    }),
  );

export const exportAuditLogsApi = (params: AuditLogQuery) =>
  requestClient.get('/audit-logs/export', { params, responseType: 'blob' });
