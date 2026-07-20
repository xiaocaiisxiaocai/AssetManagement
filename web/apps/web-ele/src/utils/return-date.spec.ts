import { describe, expect, it } from 'vitest';

import {
  disableNonExtensionReturnDate,
  disableNonFutureReturnDate,
  isFutureReturnDate,
  isValidExtensionReturnDate,
} from './return-date';

const now = new Date('2026-07-15T15:59:00Z');

describe('借用归还日期规则', () => {
  it('只接受晚于今天的有效 YYYY-MM-DD 日期', () => {
    expect(isFutureReturnDate('2026-07-14', now)).toBe(false);
    expect(isFutureReturnDate('2026-07-15', now)).toBe(false);
    expect(isFutureReturnDate('2026-07-16', now)).toBe(true);
    expect(isFutureReturnDate('2026-02-30', now)).toBe(false);
    expect(isFutureReturnDate('2026-7-16', now)).toBe(false);
  });

  it('以中国时区的零点作为业务日边界', () => {
    expect(
      isFutureReturnDate('2026-07-20', new Date('2026-07-19T15:59:59Z')),
    ).toBe(true);
    expect(
      isFutureReturnDate('2026-07-20', new Date('2026-07-19T16:00:00Z')),
    ).toBe(false);
  });

  it('日期面板禁用今天及过去日期', () => {
    expect(disableNonFutureReturnDate(new Date(2026, 6, 14), now)).toBe(true);
    expect(disableNonFutureReturnDate(new Date(2026, 6, 15), now)).toBe(true);
    expect(disableNonFutureReturnDate(new Date(2026, 6, 16), now)).toBe(false);
  });

  it('延期日期必须同时晚于今天和原应归还日期', () => {
    expect(isValidExtensionReturnDate('2026-07-20', '2026-07-20', now)).toBe(
      false,
    );
    expect(isValidExtensionReturnDate('2026-07-21', '2026-07-20', now)).toBe(
      true,
    );
    expect(isValidExtensionReturnDate('2026-07-16', '2026-07-10', now)).toBe(
      true,
    );
    expect(isValidExtensionReturnDate('2026-02-30', '2026-07-20', now)).toBe(
      false,
    );
  });

  it('延期日期面板禁用原期限及之前日期', () => {
    expect(
      disableNonExtensionReturnDate(new Date(2026, 6, 20), '2026-07-20', now),
    ).toBe(true);
    expect(
      disableNonExtensionReturnDate(new Date(2026, 6, 21), '2026-07-20', now),
    ).toBe(false);
  });
});
