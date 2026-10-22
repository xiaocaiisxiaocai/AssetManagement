import type { AssetOverview } from '#/api/asset-overview';

import { describe, expect, it } from 'vitest';

import {
  buildCategoryDistribution,
  buildCategoryStatusSeries,
} from './chart-options';

const stats: AssetOverview = {
  available: 5,
  borrowed: 2,
  maintenance: 0,
  scrapped: 0,
  total: 7,
  uncategorizedTotal: 1,
  categories: [
    {
      categoryId: 2,
      categoryCode: 'RD-PLC',
      label: 'PLC',
      parentCode: 'RD',
      remark: null,
      total: 4,
      available: 3,
      borrowed: 1,
      maintenance: 0,
      scrapped: 0,
    },
    {
      categoryId: 5,
      categoryCode: 'IT-PLC',
      label: 'PLC',
      parentCode: 'IT',
      remark: null,
      total: 2,
      available: 1,
      borrowed: 1,
      maintenance: 0,
      scrapped: 0,
    },
  ],
};

describe('资产总览二级分类图表', () => {
  it('同名二级分类按完整编码区分，未归类资产不丢失', () => {
    const distribution = buildCategoryDistribution(stats);
    expect(distribution).toEqual([
      { name: 'RD-PLC', value: 4 },
      { name: 'IT-PLC', value: 2 },
      { name: '未归入二级分类', value: 1 },
    ]);
    expect(distribution.reduce((sum, item) => sum + item.value, 0)).toBe(
      stats.total,
    );
  });

  it('在库和借出系列按相同分类顺序对应', () => {
    const series = buildCategoryStatusSeries(stats);
    expect(series).toEqual({
      labels: ['RD-PLC', 'IT-PLC'],
      available: [3, 1],
      borrowed: [1, 1],
      maintenance: [0, 0],
      scrapped: [0, 0],
    });
    expect(
      series.labels.map(
        (_, index) =>
          series.available[index]! +
          series.borrowed[index]! +
          series.maintenance[index]! +
          series.scrapped[index]!,
      ),
    ).toEqual(stats.categories.map((item) => item.total));
  });
  it('空数据可呈现', () => {
    expect(
      buildCategoryDistribution({
        ...stats,
        categories: [],
        total: 0,
        uncategorizedTotal: 0,
      }),
    ).toEqual([]);
  });
});
