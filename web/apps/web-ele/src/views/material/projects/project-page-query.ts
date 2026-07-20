import type { ProjectFilter } from './project-filter';
import type { DeleteStatus } from './project-workspace-types';

import type { TestProjectPageQuery } from '#/api/test-project';

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
    ownerId: filter.ownerId,
    page,
    pageSize,
    progressCode: filter.progressCode || undefined,
    projectTypeCode: filter.projectTypeCode || undefined,
  };
}
