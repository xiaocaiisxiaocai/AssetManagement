import type { SaveTestProjectOptionPayload, TestProjectOption } from './types';

import { requestClient } from '#/api/request';

import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export const listTestProjectOptionsApi = (kind?: string) =>
  unwrap(
    requestClient.get<ApiResult<TestProjectOption[]>>(
      '/test-projects/options',
      {
        params: { kind },
      },
    ),
  );

export const createTestProjectOptionApi = (
  data: SaveTestProjectOptionPayload,
) =>
  unwrap(
    requestClient.post<ApiResult<TestProjectOption>>(
      '/test-projects/options',
      data,
    ),
  );

export const updateTestProjectOptionApi = (
  id: number,
  data: SaveTestProjectOptionPayload,
) =>
  unwrap(
    requestClient.put<ApiResult<TestProjectOption>>(
      `/test-projects/options/${id}`,
      data,
    ),
  );

export const deleteTestProjectOptionApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/options/${id}`));
