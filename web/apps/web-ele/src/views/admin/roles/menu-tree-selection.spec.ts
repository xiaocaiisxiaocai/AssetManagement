import { describe, expect, it } from 'vitest';

import { mergeMenuTreeSelection } from './menu-tree-selection';

describe('菜单授权树选中结果', () => {
  it('保存时包含全选节点和半选父节点，并去重', () => {
    expect(mergeMenuTreeSelection([11, 12, 12], [10, 7])).toEqual([11, 12, 10, 7]);
  });

  it('父级取消后没有子级选中时不会保留旧子菜单', () => {
    expect(mergeMenuTreeSelection([], [])).toEqual([]);
  });
});
