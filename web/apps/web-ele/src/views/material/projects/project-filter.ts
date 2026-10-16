import type { TestProjectItem } from '#/api/test-project';

export type ProjectFilter = {
  code: string;
  name: string;
  ownerIds: number[];
  progressCodes: string[];
  projectTypeCodes: string[];
};

function includesText(value: null | string | undefined, keyword: string) {
  const text = keyword.trim().toLowerCase();
  if (!text) return true;
  return (value ?? '').toLowerCase().includes(text);
}

export function filterProjects(
  projects: TestProjectItem[],
  filter: ProjectFilter,
) {
  return projects.filter(
    (project) =>
      includesText(project.code, filter.code) &&
      includesText(project.name, filter.name) &&
      (filter.projectTypeCodes.length === 0 ||
        filter.projectTypeCodes.includes(project.projectTypeCode ?? '')) &&
      (filter.progressCodes.length === 0 ||
        filter.progressCodes.includes(project.progressCode ?? '')) &&
      (filter.ownerIds.length === 0 ||
        (project.ownerId !== undefined &&
          project.ownerId !== null &&
          filter.ownerIds.includes(project.ownerId))),
  );
}
