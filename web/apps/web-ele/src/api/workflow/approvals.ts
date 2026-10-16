import type {
  AddSignPayload,
  ApprovalActionPayload,
  ApprovalFlow,
  ApprovalPageQuery,
  PagedResult,
  RejectPayload,
  StartApprovalPayload,
} from './types';

import { requestClient } from '#/api/request';

import { repeatArrayParams } from '../repeat-params';
import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export const startApprovalApi = (data: StartApprovalPayload) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>('/approvals', data));

export const getPendingApprovalsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/pending'));

export const getMineApprovalsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/mine'));

export const getPendingApprovalsPageApi = (params: ApprovalPageQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<ApprovalFlow>>>(
      '/approvals/pending-page',
      { ...repeatArrayParams, params },
    ),
  );

export const getHandledApprovalsPageApi = (params: ApprovalPageQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<ApprovalFlow>>>(
      '/approvals/handled-page',
      { ...repeatArrayParams, params },
    ),
  );

export const getMineApprovalsPageApi = (params: ApprovalPageQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<ApprovalFlow>>>(
      '/approvals/mine-page',
      { ...repeatArrayParams, params },
    ),
  );

// 待接收确认:已审批通过、尚未确认接收归还的借用单
export const getPendingReturnsApi = () =>
  unwrap(
    requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/pending-return'),
  );

export const getPendingReturnsPageApi = (
  params: Pick<
    ApprovalPageQuery,
    'keyword' | 'page' | 'pageSize' | 'returnDate'
  >,
) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<ApprovalFlow>>>(
      '/approvals/pending-return-page',
      { params },
    ),
  );

export const getFlowDetailApi = (id: number) =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow>>(`/approvals/${id}`));

export const approveFlowApi = (id: number, data: ApprovalActionPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/approve`,
      data,
    ),
  );

export const rejectFlowApi = (id: number, data: RejectPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/reject`,
      data,
    ),
  );

export const withdrawApprovalApi = (id: number) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/withdraw`,
      {},
    ),
  );

export const addSignFlowApi = (id: number, data: AddSignPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/add-sign`,
      data,
    ),
  );

export const cancelAddSignFlowApi = (id: number, data: AddSignPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/cancel-add-sign`,
      data,
    ),
  );

export const confirmReturnApi = (id: number) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/confirm-return`,
      {},
    ),
  );
