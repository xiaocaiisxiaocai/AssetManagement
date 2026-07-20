import { describe, expect, it, vi } from 'vitest';

import { createObjectUrlLifecycle } from './object-url-lifecycle';

describe('对象 URL 生命周期', () => {
  it('关闭后立即回收晚到的上传预览 URL', () => {
    const revoke = vi.fn();
    const lifecycle = createObjectUrlLifecycle(revoke);
    const uploadGeneration = lifecycle.open();

    lifecycle.close();

    expect(lifecycle.adopt('blob:late-preview', uploadGeneration)).toBe(false);
    expect(revoke).toHaveBeenCalledWith('blob:late-preview');
  });

  it('关闭时回收当前会话拥有的 URL', () => {
    const revoke = vi.fn();
    const lifecycle = createObjectUrlLifecycle(revoke);
    const generation = lifecycle.open();
    lifecycle.adopt('blob:current-preview', generation);

    lifecycle.close();

    expect(revoke).toHaveBeenCalledWith('blob:current-preview');
  });

  it('关闭后重新打开时拒绝旧请求进入新会话', () => {
    const revoke = vi.fn();
    const lifecycle = createObjectUrlLifecycle(revoke);
    const oldGeneration = lifecycle.open();
    lifecycle.close();
    const newGeneration = lifecycle.open();

    expect(lifecycle.adopt('blob:old-request', oldGeneration)).toBe(false);
    expect(lifecycle.adopt('blob:new-request', newGeneration)).toBe(true);
    expect(revoke).toHaveBeenCalledWith('blob:old-request');
    expect(revoke).not.toHaveBeenCalledWith('blob:new-request');
  });
});
