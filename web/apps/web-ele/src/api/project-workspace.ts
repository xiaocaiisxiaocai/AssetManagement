import { requestClient } from '#/api/request';

import { unwrap } from './unwrap';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export type ProjectStageStatus =
  | 'blocked'
  | 'completed'
  | 'in_progress'
  | 'planned';

export interface ProjectStageDto {
  endDate?: null | string;
  id: number;
  name: string;
  objective?: null | string;
  projectId: number;
  rowVersion: number;
  sequence: number;
  startDate?: null | string;
  status: ProjectStageStatus;
  testContent?: null | string;
  testResult?: null | string;
  updatedAt: string;
}

export interface ProjectDocumentDto {
  contentType: string;
  description?: null | string;
  fileName: string;
  id: number;
  projectId: number;
  size: number;
  stageId?: null | number;
  uploadedAt: string;
  uploadedBy: string;
}

export interface ProjectWorkspaceDto {
  canManage: boolean;
  canExport: boolean;
  documents: ProjectDocumentDto[];
  projectId: number;
  stages: ProjectStageDto[];
}

export interface SaveProjectStagePayload {
  endDate?: null | string;
  name: string;
  objective?: null | string;
  rowVersion: number;
  sequence: number;
  startDate?: null | string;
  status: ProjectStageStatus;
  testContent?: null | string;
  testResult?: null | string;
}

const workspaceUrl = (projectId: number) =>
  `/test-projects/${projectId}/workspace`;

export const getProjectWorkspaceApi = (projectId: number) =>
  unwrap(
    requestClient.get<ApiResult<ProjectWorkspaceDto>>(workspaceUrl(projectId)),
  );

export const createProjectStageApi = (
  projectId: number,
  data: SaveProjectStagePayload,
) =>
  unwrap(
    requestClient.post<ApiResult<ProjectStageDto>>(
      `${workspaceUrl(projectId)}/stages`,
      data,
    ),
  );

export const updateProjectStageApi = (
  projectId: number,
  stageId: number,
  data: SaveProjectStagePayload,
) =>
  unwrap(
    requestClient.put<ApiResult<ProjectStageDto>>(
      `${workspaceUrl(projectId)}/stages/${stageId}`,
      data,
    ),
  );

export const deleteProjectStageApi = (
  projectId: number,
  stageId: number,
  rowVersion: number,
) =>
  unwrap(
    requestClient.delete<ApiResult<null>>(
      `${workspaceUrl(projectId)}/stages/${stageId}`,
      { params: { rowVersion } },
    ),
  );

export const uploadProjectDocumentApi = (
  projectId: number,
  file: File,
  stageId?: null | number,
  description?: null | string,
) => {
  const form = new FormData();
  form.append('file', file);
  if (stageId !== undefined && stageId !== null)
    form.append('stageId', String(stageId));
  if (description?.trim()) form.append('description', description.trim());
  return unwrap(
    requestClient.post<ApiResult<ProjectDocumentDto>>(
      `${workspaceUrl(projectId)}/documents`,
      form,
    ),
  );
};

export const deleteProjectDocumentApi = (
  projectId: number,
  documentId: number,
) =>
  unwrap(
    requestClient.delete<ApiResult<null>>(
      `${workspaceUrl(projectId)}/documents/${documentId}`,
    ),
  );

export const downloadProjectDocumentApi = (
  projectId: number,
  documentId: number,
) =>
  requestClient.get(
    `${workspaceUrl(projectId)}/documents/${documentId}/download`,
    { responseType: 'blob' },
  );

export const downloadProjectReportApi = (projectId: number, stageId?: number) =>
  requestClient.get(`${workspaceUrl(projectId)}/report`, {
    params: stageId === undefined ? undefined : { stageId },
    responseType: 'blob',
  });

export const downloadProjectPackageApi = (
  projectId: number,
  stageId?: number,
) =>
  requestClient.get(`${workspaceUrl(projectId)}/package`, {
    params: stageId === undefined ? undefined : { stageId },
    responseType: 'blob',
  });
