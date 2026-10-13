import type {
  SaveTestProjectFollowupPayload,
  TestProjectFollowup,
} from './types';

import { requestClient } from '#/api/request';

import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

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
