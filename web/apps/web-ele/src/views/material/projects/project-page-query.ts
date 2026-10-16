import type { ProjectFilter } from './project-filter';
import type { DeleteStatus } from './project-workspace-types';

import type { TestProjectPageQuery } from '#/api/test-project';

import { nonempty } from '#/api/repeat-params';

export function buildProjectPageQuery(
  filter: ProjectFilter,
  deleteStatus: DeleteStatus,
  page: number,
  pageSize: number,
): TestProjectPageQuery {
  return {
    code: filter.code.trim() || undefined,
    deleteStatus,
    name: filter.name.trim() || undefined,
    ownerIds: nonempty(filter.ownerIds),
    page,
    pageSize,
    progressCodes: nonempty(filter.progressCodes),
    projectTypeCodes: nonempty(filter.projectTypeCodes),
  };
}
