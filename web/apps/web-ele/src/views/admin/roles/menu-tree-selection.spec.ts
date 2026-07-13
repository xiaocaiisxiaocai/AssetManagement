import { describe, expect, it } from 'vitest';

import {
  collectRequiredPermissionIds,
  filterPageMenuTree,
  mergeMenuTreeSelection,
} from './menu-tree-selection';

describe('菜单授权树选中结果', () => {
  it('保存时包含全选节点和半选父节点，并去重', () => {
    expect(mergeMenuTreeSelection([11, 12, 12], [10, 7])).toEqual([
      11, 12, 10, 7,
    ]);
  });

  it('父级取消后没有子级选中时不会保留旧子菜单', () => {
    expect(mergeMenuTreeSelection([], [])).toEqual([]);
  });

  it('勾选菜单时能找出该页面要求的最低访问权限', () => {
    const menus = [
      {
        id: 1,
        name: 'Asset',
        sort: 1,
        type: 'menu',
        children: [
          {
            id: 2,
            name: 'AssetList',
            permissionCode: 'asset:view',
            sort: 2,
            type: 'menu',
          },
        ],
      },
    ];
    const permissions = [
      { id: 11, code: 'asset:view', name: '查看资产', module: 'asset' },
    ];

    expect(collectRequiredPermissionIds(menus, permissions, [1, 2])).toEqual([
      11,
    ]);
  });

  it('菜单范围不展示按钮节点', () => {
    const menus = [
      {
        id: 1,
        name: 'Asset',
        sort: 1,
        type: 'menu',
        children: [
          { id: 2, name: 'AssetList', sort: 2, type: 'menu' },
          { id: 3, name: 'AssetCreate', sort: 3, type: 'button' },
        ],
      },
    ];

    expect(
      filterPageMenuTree(menus)[0]?.children?.map((item) => item.id),
    ).toEqual([2]);
  });
});
