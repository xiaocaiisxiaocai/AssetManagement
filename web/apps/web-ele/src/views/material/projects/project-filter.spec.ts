import { describe, expect, it } from 'vitest';

import type { TestProjectItem } from '#/api/test-project';

import { filterProjects, type ProjectFilter } from './project-filter';

const baseProject: TestProjectItem = {
  canWriteFollowUp: false,
  code: 'CODX-NT-001',
  createdAt: '2026-06-29T00:00:00',
  followUpIntervalDays: 7,
  followUpStatus: 'upcoming',
  id: 1,
  isDeleted: false,
  materialCount: 1,
  name: 'CODX-20260629 New Pro',
  ownerId: 10,
  ownerName: 'CODX-RD-DeptAdmin',
  progressCode: 'planning',
  progressLabel: '计划中',
  projectTypeCode: 'sample',
  projectTypeLabel: '样机测试',
};

const emptyFilter: ProjectFilter = {
  code: '',
  name: '',
  ownerId: undefined,
  progressCode: '',
  projectTypeCode: '',
};

describe('测试项目筛选', () => {
  it('按项目编号和项目名称模糊匹配', () => {
    const projects = [
      baseProject,
      { ...baseProject, code: 'CODX-NT-002', id: 2, name: '量产验证项目' },
    ];

    expect(filterProjects(projects, { ...emptyFilter, code: 'nt-001' })).toEqual([baseProject]);
    expect(filterProjects(projects, { ...emptyFilter, name: '量产' })).toEqual([projects[1]]);
  });

  it('按项目类型、负责人和进度精确匹配', () => {
    const projects = [
      baseProject,
      {
        ...baseProject,
        id: 2,
        ownerId: 11,
        progressCode: 'testing',
        projectTypeCode: 'trial',
      },
    ];

    const result = filterProjects(projects, {
      ...emptyFilter,
      ownerId: 11,
      progressCode: 'testing',
      projectTypeCode: 'trial',
    });

    expect(result).toEqual([projects[1]]);
  });
});
