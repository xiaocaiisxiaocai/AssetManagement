import { describe, expect, it } from 'vitest';

import { mapWithConcurrency } from './bounded-concurrency';

describe('有界并发', () => {
  it('输出保持原始顺序且同时任务不超过上限', async () => {
    let active = 0;
    let maxActive = 0;
    const result = await mapWithConcurrency(
      [1, 2, 3, 4, 5, 6],
      2,
      async (item) => {
        active += 1;
        maxActive = Math.max(maxActive, active);
        await Promise.resolve();
        active -= 1;
        return item * 2;
      },
    );
    expect(maxActive).toBeLessThanOrEqual(2);
    expect(result).toEqual([2, 4, 6, 8, 10, 12]);
  });
});
