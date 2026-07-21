import { describe, expect, it } from 'vitest';

import {
  approvalDashboardCounts,
  combineAvailableCounts,
} from './approval-counts';

describe('首页审批数量', () => {
  it('同时计入固定资产和测试料件流程', () => {
    expect(approvalDashboardCounts(1, 2, 1, 1)).toEqual({
      minePending: 2,
      pending: 3,
    });
  });

  it('任一有权数据源未加载时不把部分结果显示为真实总数', () => {
    expect(
      combineAvailableCounts([
        { enabled: true, value: null },
        { enabled: true, value: 0 },
        { enabled: false, value: null },
      ]),
    ).toBeNull();

    expect(
      combineAvailableCounts([
        { enabled: true, value: 2 },
        { enabled: true, value: 3 },
      ]),
    ).toBe(5);
  });
});
