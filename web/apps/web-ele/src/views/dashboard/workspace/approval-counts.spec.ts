import { describe, expect, it } from 'vitest';

import { approvalDashboardCounts } from './approval-counts';

describe('首页审批数量', () => {
  it('同时计入固定资产和测试料件流程', () => {
    expect(approvalDashboardCounts(1, 2, 1, 1)).toEqual({
      minePending: 2,
      pending: 3,
    });
  });
});
