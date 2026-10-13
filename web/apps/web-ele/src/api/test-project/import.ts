import type { TestProjectImportResult } from './types';

import { requestClient } from '#/api/request';

import { unwrap } from '../unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export const downloadTestProjectImportTemplateApi = () =>
  requestClient.get('/test-projects/import/template', {
    responseType: 'blob',
  });

export const validateTestProjectImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<TestProjectImportResult>>(
      '/test-projects/import/validate',
      form,
    ),
  );
};

export const confirmTestProjectImportApi = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return unwrap(
    requestClient.post<ApiResult<TestProjectImportResult>>(
      '/test-projects/import/confirm',
      form,
    ),
  );
};
