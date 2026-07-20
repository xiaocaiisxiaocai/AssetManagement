import { describe, expect, it, vi } from 'vitest';

import { runHandled } from './handled-promise';

describe('事件触发的异步任务', () => {
  it('消费请求层已提示的 Promise 拒绝', async () => {
    const unhandled = vi.fn();
    window.addEventListener('unhandledrejection', unhandled);

    runHandled(Promise.reject(new Error('network')));
    await Promise.resolve();
    await Promise.resolve();

    expect(unhandled).not.toHaveBeenCalled();
    window.removeEventListener('unhandledrejection', unhandled);
  });
});
