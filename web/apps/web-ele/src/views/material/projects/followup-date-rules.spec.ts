import { describe, expect, it } from 'vitest';

import { isFutureFollowupDate } from './followup-date-rules';

describe('落地跟进日期规则', () => {
  const today = new Date(2026, 6, 22, 12, 0, 0);

  it('允许今天和过去，禁止未来日期', () => {
    expect(isFutureFollowupDate(new Date(2026, 6, 21), today)).toBe(false);
    expect(isFutureFollowupDate(new Date(2026, 6, 22, 23, 59), today)).toBe(
      false,
    );
    expect(isFutureFollowupDate(new Date(2026, 6, 23), today)).toBe(true);
  });
});
