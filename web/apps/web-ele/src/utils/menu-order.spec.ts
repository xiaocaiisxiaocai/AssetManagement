import { describe, expect, it } from 'vitest';

import { sortBuiltInMenus } from './menu-order';

describe('内置菜单排序', () => {
  it('资产总览位于资产列表上方', () => {
    const [asset] = sortBuiltInMenus([
      {
        name: 'Asset',
        children: [
          { name: 'AssetList', sort: 1 },
          { name: 'AssetOverview', sort: 99 },
        ],
      },
    ]);
    expect(asset?.children?.map((item) => item.name)).toEqual([
      'AssetOverview',
      'AssetList',
    ]);
  });

  it('系统管理子菜单按业务顺序排序', () => {
    const [admin] = sortBuiltInMenus([
      {
        name: 'Admin',
        children: [
          { name: 'AdminBackups', sort: 1 },
          { name: 'AdminAudit', sort: 2 },
          { name: 'AdminSettings', sort: 3 },
          { name: 'AdminDepartments', sort: 4 },
          { name: 'AssetCategories', sort: 9 },
          { name: 'AdminRoles', sort: 5 },
          { name: 'AdminUsers', sort: 6 },
          { name: 'AdminWorkflows', sort: 7 },
        ],
      },
    ]);

    expect(admin?.children?.map((item) => item.name)).toEqual([
      'AdminUsers',
      'AdminRoles',
      'AdminDepartments',
      'AssetCategories',
      'AdminWorkflows',
      'AdminSettings',
      'AdminAudit',
      'AdminBackups',
    ]);
  });
});
