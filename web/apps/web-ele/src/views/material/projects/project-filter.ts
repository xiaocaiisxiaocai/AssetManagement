import type { TestProjectItem } from '#/api/test-project';

export type ProjectFilter = {
  code: string;
  name: string;
  ownerId?: number;
  progressCode: string;
  projectTypeCode: string;
};

function includesText(value: null | string | undefined, keyword: string) {
  const text = keyword.trim().toLowerCase();
  if (!text) return true;
  return (value ?? '').toLowerCase().includes(text);
}

export function filterProjects(projects: TestProjectItem[], filter: ProjectFilter) {
  return projects.filter((project) =>
    includesText(project.code, filter.code) &&
    includesText(project.name, filter.name) &&
    (!filter.projectTypeCode || project.projectTypeCode === filter.projectTypeCode) &&
    (!filter.progressCode || project.progressCode === filter.progressCode) &&
    (!filter.ownerId || project.ownerId === filter.ownerId),
  );
}
