import { describe, expect, it } from 'vitest';

import { mergeSettledValue } from './dashboard-load-state';

describe('仪表盘加载状态', () => {
  it('首次加载失败时保持未加载状态，不伪装成零值', () => {
    const snapshot = mergeSettledValue<number>(null, {
      status: 'rejected',
      reason: new Error('网络不可用'),
    });

    expect(snapshot).toEqual({ error: '网络不可用', value: null });
  });

  it('刷新失败时保留最近一次成功数据', () => {
    const snapshot = mergeSettledValue(12, {
      status: 'rejected',
      reason: '超时',
    });

    expect(snapshot).toEqual({ error: '加载失败', value: 12 });
  });

  it('重试成功后覆盖旧数据并清除错误', () => {
    const snapshot = mergeSettledValue(12, {
      status: 'fulfilled',
      value: 18,
    });

    expect(snapshot).toEqual({ error: null, value: 18 });
  });
});
