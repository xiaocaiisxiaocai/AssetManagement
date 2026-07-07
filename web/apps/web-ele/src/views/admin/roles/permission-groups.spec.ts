import type { MenuDto, PermissionDto } from '#/api/role';

import { describe, expect, it } from 'vitest';

import { buildPermissionGroups } from './permission-groups';

const permissions: PermissionDto[] = [
  { id: 1, code: 'project:view', name: '查看项目', module: 'project' },
  { id: 2, code: 'project:create', name: '新增项目', module: 'project' },
  { id: 3, code: 'material:view', name: '查看料件', module: 'material' },
  { id: 4, code: 'material-flow:approve', name: '审批流转', module: 'material-flow' },
];

const menus: MenuDto[] = [
  {
    id: 1,
    name: 'Material',
    title: '新产品新技术',
    sort: 1,
    type: 'menu',
    children: [
      {
        id: 2,
        name: 'MaterialHome',
        title: '项目总览',
        sort: 1,
        type: 'menu',
      },
      {
        id: 3,
        name: 'MaterialProjects',
        title: '测试项目',
        sort: 2,
        type: 'menu',
      },
    ],
  },
];

describe('角色权限分组', () => {
  it('父菜单不重复展示子菜单已覆盖的权限', () => {
    const groups = buildPermissionGroups({
      menus,
      permissions,
      selectedPermissionIds: [1, 2, 3, 4],
    });

    expect(groups.map((group) => [group.label, group.total])).toEqual([
      ['项目总览', 2],
      ['测试项目', 4],
    ]);
  });
});
