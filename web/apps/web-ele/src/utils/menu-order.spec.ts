import { describe, expect, it } from 'vitest';

import { sortBuiltInMenus } from './menu-order';

describe('内置菜单排序', () => {
  it('系统管理子菜单按业务顺序排序', () => {
    const [admin] = sortBuiltInMenus([
      {
        name: 'Admin',
        children: [
          { name: 'AdminBackups', sort: 1 },
          { name: 'AdminAudit', sort: 2 },
          { name: 'AdminSettings', sort: 3 },
          { name: 'AdminDepartments', sort: 4 },
          { name: 'AdminRoles', sort: 5 },
          { name: 'AdminUsers', sort: 6 },
          { name: 'AdminWorkflows', sort: 7 },
        ],
      },
    ]);

    expect(admin!.children!.map((item) => item.name)).toEqual([
      'AdminUsers',
      'AdminRoles',
      'AdminDepartments',
      'AdminWorkflows',
      'AdminSettings',
      'AdminAudit',
      'AdminBackups',
    ]);
  });
});
