import { describe, expect, it } from 'vitest';

import { createLatestRequestGuard } from './latest-request';

describe('最新请求代次', () => {
  it('只允许最后一次请求写回状态', () => {
    const guard = createLatestRequestGuard();
    const first = guard.next();
    const second = guard.next();

    expect(guard.isLatest(first)).toBe(false);
    expect(guard.isLatest(second)).toBe(true);
  });

  it('关闭页面后可让未完成请求失效', () => {
    const guard = createLatestRequestGuard();
    const request = guard.next();
    guard.invalidate();
    expect(guard.isLatest(request)).toBe(false);
  });
});
