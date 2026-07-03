import type { DepartmentNode } from '#/api/base-data';

import { describe, expect, it } from 'vitest';

import { flattenActiveDepartments } from './department-options';

describe('flattenActiveDepartments', () => {
  it('不显示停用部门及其下级部门', () => {
    const departments: DepartmentNode[] = [
      {
        assetCount: 0,
        children: [
          {
            assetCount: 0,
            children: [],
            id: 2,
            isActive: true,
            managerName: null,
            name: '停用父级下的启用子级',
            parentId: 1,
          },
        ],
        id: 1,
        isActive: false,
        managerName: null,
        name: '停用父级',
        parentId: null,
      },
      {
        assetCount: 0,
        children: [],
        id: 3,
        isActive: true,
        managerName: null,
        name: '可用部门',
        parentId: null,
      },
    ];

    expect(flattenActiveDepartments(departments)).toEqual([
      {
        id: 3,
        isActive: true,
        label: '可用部门',
      },
    ]);
  });
});
