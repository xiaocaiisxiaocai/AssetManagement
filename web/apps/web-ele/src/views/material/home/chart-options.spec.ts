import { describe, expect, it } from 'vitest';

import {
  buildMonthlySeriesData,
  buildMonthlyTableRows,
  buildStatusDistribution,
  monthLabels,
  quantityAxisLabel,
} from './chart-options';

describe('测试项目总览图表配置', () => {
  it('固定展示 1 到 12 月，并按 month 字段对齐月度数据', () => {
    const result = buildMonthlySeriesData([
      { month: 3, closedCount: 2, followUpCount: 1 },
      { month: 1, closedCount: 4, followUpCount: 0 },
      { month: 0, closedCount: 9, followUpCount: 9 },
      { month: 13, closedCount: 9, followUpCount: 9 },
    ]);

    expect(monthLabels).toEqual([
      '1月',
      '2月',
      '3月',
      '4月',
      '5月',
      '6月',
      '7月',
      '8月',
      '9月',
      '10月',
      '11月',
      '12月',
    ]);
    expect(result.closedData).toEqual([4, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
    expect(result.followUpData).toEqual([0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
  });

  it('数量轴标签带单位，避免 0 和 1月 连在一起被误读成 0月', () => {
    expect(quantityAxisLabel.formatter(0)).toBe('0个');
    expect(quantityAxisLabel.formatter(12)).toBe('12个');
  });

  it('为图表生成等价的语义化数据表', () => {
    expect(
      buildStatusDistribution({ closed: 3, inProgress: 4, landed: 2 }),
    ).toEqual([
      { label: '计划/测试中', value: 4 },
      { label: '结案', value: 3 },
      { label: '落地跟进', value: 2 },
    ]);

    const rows = buildMonthlyTableRows([
      { month: 2, closedCount: 5, followUpCount: 3 },
    ]);
    expect(rows).toHaveLength(12);
    expect(rows[1]).toEqual({
      closedCount: 5,
      followUpCount: 3,
      month: '2月',
    });
  });
});
