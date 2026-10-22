import type {
  PagedResult,
  SaveTestProjectPayload,
  TestProjectItem,
  TestProjectPageQuery,
  UpdateTestProjectProgressPayload,
} from './types';

import { requestClient } from '#/api/request';

import { repeatArrayParams } from '../repeat-params';
import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export const listTestProjectsApi = (deleteStatus?: string) =>
  unwrap(
    requestClient.get<ApiResult<TestProjectItem[]>>('/test-projects', {
      params: { deleteStatus },
    }),
  );

export const listTestProjectsPageApi = (params: TestProjectPageQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<TestProjectItem>>>(
      '/test-projects/page',
      { ...repeatArrayParams, params },
    ),
  );

export const exportTestProjectsApi = (params: TestProjectPageQuery) =>
  requestClient.get('/test-projects/export', {
    ...repeatArrayParams,
    params,
    responseType: 'blob',
  });

export const createTestProjectApi = (data: SaveTestProjectPayload) =>
  unwrap(
    requestClient.post<ApiResult<TestProjectItem>>('/test-projects', data),
  );

export const updateTestProjectApi = (
  id: number,
  data: SaveTestProjectPayload,
) =>
  unwrap(
    requestClient.put<ApiResult<TestProjectItem>>(`/test-projects/${id}`, data),
  );

export const updateTestProjectProgressApi = (
  id: number,
  data: UpdateTestProjectProgressPayload,
) =>
  unwrap(
    requestClient.put<ApiResult<TestProjectItem>>(
      `/test-projects/${id}/progress`,
      data,
    ),
  );

export const reopenTestProjectApi = (id: number, rowVersion?: number) =>
  unwrap(
    requestClient.post<ApiResult<TestProjectItem>>(
      `/test-projects/${id}/reopen`,
      { rowVersion },
    ),
  );

export const deleteTestProjectApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/${id}`));

export const restoreTestProjectApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/test-projects/${id}/restore`));

export const purgeTestProjectApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/${id}/purge`));
