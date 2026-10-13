import type {
  SaveWorkflowPayload,
  WorkflowDesignerOptions,
  WorkflowItem,
} from './types';

import { requestClient } from '#/api/request';

import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export const getWorkflowsApi = () =>
  unwrap(requestClient.get<ApiResult<WorkflowItem[]>>('/workflows'));

export const getWorkflowApi = (id: number) =>
  unwrap(requestClient.get<ApiResult<WorkflowItem>>(`/workflows/${id}`));

export const createWorkflowApi = (data: SaveWorkflowPayload) =>
  unwrap(requestClient.post<ApiResult<WorkflowItem>>('/workflows', data));

export const saveWorkflowApi = (id: number, data: SaveWorkflowPayload) =>
  unwrap(requestClient.put<ApiResult<WorkflowItem>>(`/workflows/${id}`, data));

export const saveWorkflowDesignApi = (id: number, bpmnXml: null | string) =>
  unwrap(
    requestClient.put<ApiResult<WorkflowItem>>(`/workflows/${id}/design`, {
      bpmnXml,
    }),
  );

export const setWorkflowStatusApi = (id: number, isActive: boolean) =>
  unwrap(
    requestClient.post<ApiResult<WorkflowItem>>(`/workflows/${id}/status`, {
      isActive,
    }),
  );

export const deleteWorkflowApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<boolean>>(`/workflows/${id}`));

export const getWorkflowDesignerOptionsApi = (
  keyword = '',
  page = 1,
  pageSize = 50,
) =>
  unwrap(
    requestClient.get<ApiResult<WorkflowDesignerOptions>>(
      '/workflow-designer/options',
      { params: { keyword, page, pageSize } },
    ),
  );
