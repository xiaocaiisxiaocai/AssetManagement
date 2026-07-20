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

export interface TestProjectPageQuery {
  code?: string;
  deleteStatus?: string;
  name?: string;
  ownerId?: number;
  page: number;
  pageSize: number;
  progressCode?: string;
  projectTypeCode?: string;
}

export interface TestProjectItem {
  id: number;
  name: string;
  code?: null | string;
  projectTypeCode?: null | string;
  projectTypeLabel?: null | string;
  startDate?: null | string;
  plannedFinishDate?: null | string;
  closedDate?: null | string;
  progressCode?: null | string;
  progressLabel?: null | string;
  ownerId?: null | number;
  ownerName?: null | string;
  testStatus?: null | string;
  followUpIntervalDays: number;
  nextFollowUpDueDate?: null | string;
  followUpStatus: 'due' | 'overdue' | 'upcoming' | string;
  latestFollowUpAt?: null | string;
  latestFollowUpContent?: null | string;
  canWriteFollowUp: boolean;
  createdAt: string;
  isDeleted: boolean;
  deletedAt?: null | string;
  materialCount: number;
}

export interface SaveTestProjectPayload {
  name: string;
  code?: null | string;
  projectTypeCode?: null | string;
  startDate?: null | string;
  plannedFinishDate?: null | string;
  closedDate?: null | string;
  progressCode?: null | string;
  ownerId?: null | number;
  testStatus?: null | string;
  followUpIntervalDays: number;
}

export interface TestProjectOption {
  id: number;
  kind: 'project_progress' | 'project_type';
  code: string;
  label: string;
  sort: number;
  isActive: boolean;
}

export interface SaveTestProjectOptionPayload {
  kind: 'project_progress' | 'project_type';
  code: string;
  label: string;
  sort: number;
  isActive: boolean;
}

export interface TestProjectFollowup {
  id: number;
  projectId: number;
  dueDate: string;
  content: string;
  filledById: number;
  filledByName?: null | string;
  filledAt: string;
}

export interface SaveTestProjectFollowupPayload {
  content: string;
  dueDate?: null | string;
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

export const listTestProjectsPageApi = (params: TestProjectPageQuery) =>
  unwrap(
    requestClient.get<ApiResult<PagedResult<TestProjectItem>>>(
      '/test-projects/page',
      { params },
    ),
  );

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

export const deleteTestProjectApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/${id}`));

export const restoreTestProjectApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<null>>(`/test-projects/${id}/restore`));

export const purgeTestProjectApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<null>>(`/test-projects/${id}/purge`));

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

export const listTestProjectFollowupsApi = (projectId: number) =>
  unwrap(
    requestClient.get<ApiResult<TestProjectFollowup[]>>(
      `/test-projects/${projectId}/followups`,
    ),
  );

export const createTestProjectFollowupApi = (
  projectId: number,
  data: SaveTestProjectFollowupPayload,
) =>
  unwrap(
    requestClient.post<ApiResult<TestProjectFollowup>>(
      `/test-projects/${projectId}/followups`,
      data,
    ),
  );

export const updateTestProjectFollowupApi = (
  projectId: number,
  followupId: number,
  data: SaveTestProjectFollowupPayload,
) =>
  unwrap(
    requestClient.put<ApiResult<TestProjectFollowup>>(
      `/test-projects/${projectId}/followups/${followupId}`,
      data,
    ),
  );

export const deleteTestProjectFollowupApi = (
  projectId: number,
  followupId: number,
) =>
  unwrap(
    requestClient.delete<ApiResult<null>>(
      `/test-projects/${projectId}/followups/${followupId}`,
    ),
  );

export interface TestProjectStats {
  total: number;
  closed: number;
  inProgress: number;
  landed: number;
  typeDist: { count: number; label: string }[];
  monthlyStat: { closedCount: number; followUpCount: number; month: number }[];
}

export const getTestProjectStatsApi = () =>
  unwrap(
    requestClient.get<ApiResult<TestProjectStats>>('/test-projects/stats'),
  );
