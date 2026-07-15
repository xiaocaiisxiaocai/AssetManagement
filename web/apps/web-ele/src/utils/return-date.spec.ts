import { describe, expect, it } from 'vitest';

import { disableNonFutureReturnDate, isFutureReturnDate } from './return-date';

const now = new Date(2026, 6, 15, 23, 59);

describe('借用归还日期规则', () => {
  it('只接受晚于今天的有效 YYYY-MM-DD 日期', () => {
    expect(isFutureReturnDate('2026-07-14', now)).toBe(false);
    expect(isFutureReturnDate('2026-07-15', now)).toBe(false);
    expect(isFutureReturnDate('2026-07-16', now)).toBe(true);
    expect(isFutureReturnDate('2026-02-30', now)).toBe(false);
    expect(isFutureReturnDate('2026-7-16', now)).toBe(false);
  });

  it('日期面板禁用今天及过去日期', () => {
    expect(disableNonFutureReturnDate(new Date(2026, 6, 14), now)).toBe(true);
    expect(disableNonFutureReturnDate(new Date(2026, 6, 15), now)).toBe(true);
    expect(disableNonFutureReturnDate(new Date(2026, 6, 16), now)).toBe(false);
  });
});
