import { describe, expect, it } from 'vitest';

import { endOfSelectedDay, startOfSelectedDay } from './date-range';

describe('日期区间参数', () => {
  it('开始日期包含当天零点', () => {
    expect(startOfSelectedDay('2026-07-10')).toBe('2026-07-10T00:00:00');
  });

  it('结束日期包含当天全部时间', () => {
    expect(endOfSelectedDay('2026-07-10')).toBe('2026-07-10T23:59:59.999');
  });

  it('保留已有时间并处理空值', () => {
    expect(endOfSelectedDay('2026-07-10T08:30:00')).toBe('2026-07-10T08:30:00');
    expect(startOfSelectedDay()).toBeUndefined();
  });
});
