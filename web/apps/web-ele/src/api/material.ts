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

// 0=在用 1=已退回厂商
export type MaterialStatus = 0 | 1;

export interface MaterialItem {
  id: number;
  materialNo: string;
  name: string;
  projectId: number;
  projectName?: null | string;
  vendorName?: null | string;
  model?: null | string;
  brand?: null | string;
  quantity: number;
  departmentId?: null | number;
  departmentName?: null | string;
  locationId?: null | number;
  locationName?: null | string;
  custodianId?: null | number;
  custodianName?: null | string;
  receivedDate?: null | string;
  status: MaterialStatus;
  images: string[];
  remark?: null | string;
  createdAt: string;
  isDeleted: boolean;
  deletedAt?: null | string;
  hasPendingFlow: boolean;
}

export interface MaterialQuery {
  materialNo?: string;
  name?: string;
  projectId?: null | number;
  departmentId?: null | number;
  status?: MaterialStatus | null;
  deleteStatus?: 'active' | 'all' | 'deleted';
  page?: number;
  pageSize?: number;
}

export interface SaveMaterialPayload {
  name: string;
  projectId: number;
  vendorName?: null | string;
  model?: null | string;
  brand?: null | string;
  quantity: number;
  departmentId?: null | number;
  locationId?: null | number;
  custodianId?: null | number;
  receivedDate?: null | string;
  images?: string[];
  remark?: null | string;
}

export interface MaterialFlowItem {
  id: number;
  flowNo: string;
  materialId: number;
  materialNo: string;
  materialName: string;
  applicant: string;
  applicantDept?: null | string;
  transferee?: null | string;
  transfereeDept?: null | string;
  reason?: null | string;
  status: string;
  currentNodeIds: string[];
  applyTime: string;
  directTransfer: boolean;
}

export interface MaterialFlowRecordItem {
  id: number;
  action: string;
  operator?: null | string;
  comment?: null | string;
  operatedAt: string;
}

export interface MaterialDetail {
  material: MaterialItem;
  flows: MaterialFlowItem[];
  records: MaterialFlowRecordItem[];
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const listMaterialsApi = (params: MaterialQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<MaterialItem>>>('/test-materials', {
      params,
    }),
  );

export const getMaterialDetailApi = (id: number) =>
  unwrap(
    requestClient.get<ApiResult<MaterialDetail>>(`/test-materials/${id}/detail`),
  );

export const createMaterialApi = (data: SaveMaterialPayload) =>
  unwrap(requestClient.post<ApiResult<MaterialItem>>('/test-materials', data));

export const updateMaterialApi = (id: number, data: SaveMaterialPayload) =>
  unwrap(
    requestClient.put<ApiResult<MaterialItem>>(`/test-materials/${id}`, data),
  );

export const deleteMaterialApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-materials/${id}`));

export const restoreMaterialApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/test-materials/${id}/restore`));

export const purgeMaterialApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-materials/${id}/purge`));

export const returnMaterialApi = (id: number) =>
  unwrap(
    requestClient.post<ApiResult<MaterialItem>>(`/test-materials/${id}/return`),
  );

export const initiateTransferApi = (data: {
  materialId: number;
  reason?: string;
  transfereeId: number;
}) =>
  unwrap(requestClient.post<ApiResult<MaterialFlowItem>>('/material-flows', data));

export const listPendingFlowsApi = () =>
  unwrap(
    requestClient.get<ApiResult<MaterialFlowItem[]>>('/material-flows/pending'),
  );

export const listMyFlowsApi = () =>
  unwrap(requestClient.get<ApiResult<MaterialFlowItem[]>>('/material-flows/mine'));

export const approveFlowApi = (id: number, opinion?: string) =>
  unwrap(
    requestClient.post<ApiResult<MaterialFlowItem>>(
      `/material-flows/${id}/approve`,
      { opinion },
    ),
  );

export const rejectFlowApi = (id: number, reason: string) =>
  unwrap(
    requestClient.post<ApiResult<MaterialFlowItem>>(
      `/material-flows/${id}/reject`,
      { reason },
    ),
  );
