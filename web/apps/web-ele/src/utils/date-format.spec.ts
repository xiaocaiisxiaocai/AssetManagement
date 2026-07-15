import { describe, expect, it } from 'vitest';

import { formatDate, formatDateTime } from './date-format';

describe('用户可见日期时间格式', () => {
  it('日期只展示年月日', () => {
    expect(formatDate('2026-07-15T00:54:29.764325')).toBe('2026-07-15');
    expect(formatDate(null)).toBe('-');
  });

  it('将后端无时区的 UTC 时间转换为北京时间并移除 T 和小数秒', () => {
    expect(formatDateTime('2026-07-15T00:54:29.764325')).toBe(
      '2026-07-15 08:54',
    );
    expect(
      formatDateTime('2026-07-15T00:54:29Z', { seconds: true }),
    ).toBe('2026-07-15 08:54:29');
  });

  it('尊重带偏移量的时间并处理空值或非法值', () => {
    expect(formatDateTime('2026-07-15T08:54:29+08:00')).toBe(
      '2026-07-15 08:54',
    );
    expect(formatDateTime(undefined)).toBe('-');
    expect(formatDateTime('invalid')).toBe('-');
  });
});
