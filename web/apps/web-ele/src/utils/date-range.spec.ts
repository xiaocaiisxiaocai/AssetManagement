import { describe, expect, it } from 'vitest';

import { endOfSelectedDay, startOfSelectedDay } from './date-range';

describe('日期区间参数', () => {
  it('开始日期按中国时区当天零点转换为 UTC', () => {
    expect(startOfSelectedDay('2026-07-10')).toBe('2026-07-09T16:00:00.000Z');
  });

  it('结束日期按中国时区包含当天全部时间并转换为 UTC', () => {
    expect(endOfSelectedDay('2026-07-10')).toBe('2026-07-10T15:59:59.999Z');
  });

  it('保留已有时间并处理空值', () => {
    expect(endOfSelectedDay('2026-07-10T08:30:00')).toBe('2026-07-10T08:30:00');
    expect(startOfSelectedDay()).toBeUndefined();
  });
});
