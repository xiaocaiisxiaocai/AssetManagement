import { describe, expect, it } from 'vitest';

import {
  formatNotificationDate,
  parseBackendUtcDateTime,
} from './notification-date';

describe('通知时间', () => {
  it('把后端不带时区的 DateTime 当作 UTC', () => {
    expect(parseBackendUtcDateTime('2026-07-19T08:00:00').toISOString()).toBe(
      '2026-07-19T08:00:00.000Z',
    );
  });

  it('相对时间不受运行机器本地时区影响', () => {
    expect(
      formatNotificationDate(
        '2026-07-19T08:00:00',
        new Date('2026-07-19T09:30:00Z'),
      ),
    ).toBe('1小时前');
  });
});
