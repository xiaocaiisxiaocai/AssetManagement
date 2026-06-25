import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export interface TestProjectItem {
  id: number;
  name: string;
  code?: null | string;
  description?: null | string;
  createdAt: string;
  isDeleted: boolean;
  deletedAt?: null | string;
  materialCount: number;
}

export interface SaveTestProjectPayload {
  name: string;
  code?: null | string;
  description?: null | string;
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const listTestProjectsApi = (deleteStatus?: string) =>
  unwrap(
    requestClient.get<ApiResult<TestProjectItem[]>>('/test-projects', {
      params: { deleteStatus },
    }),
  );

export const createTestProjectApi = (data: SaveTestProjectPayload) =>
  unwrap(requestClient.post<ApiResult<TestProjectItem>>('/test-projects', data));

export const updateTestProjectApi = (id: number, data: SaveTestProjectPayload) =>
  unwrap(
    requestClient.put<ApiResult<TestProjectItem>>(`/test-projects/${id}`, data),
  );

export const deleteTestProjectApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/${id}`));

export const restoreTestProjectApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/test-projects/${id}/restore`));

export const purgeTestProjectApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/${id}/purge`));
