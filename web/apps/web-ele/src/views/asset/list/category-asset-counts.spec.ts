import { describe, expect, it } from 'vitest';

import { countCategoryTreeAssets } from './category-asset-counts';

describe('资产分类聚合计数', () => {
  it('使用直接分类计数递归汇总子树', () => {
    const tree = {
      children: [
        { children: [], id: 2 },
        { children: [{ children: [], id: 4 }], id: 3 },
      ],
      id: 1,
    };

    expect(
      countCategoryTreeAssets(tree, { '1': 1, '2': 2, '3': 3, '4': 4 }),
    ).toBe(10);
  });
});
