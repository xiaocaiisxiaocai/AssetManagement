import type { TestProjectStats } from './types';

import { requestClient } from '#/api/request';

import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export const getTestProjectStatsApi = () =>
  unwrap(
    requestClient.get<ApiResult<TestProjectStats>>('/test-projects/stats'),
  );
