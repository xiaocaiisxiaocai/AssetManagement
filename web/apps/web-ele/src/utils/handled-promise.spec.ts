import { describe, expect, it, vi } from 'vitest';

import { runHandled } from './handled-promise';

describe('事件触发的异步任务', () => {
  it.each([new Error('network'), { message: '角色已配置权限，不能删除' }])(
    '消费请求层已提示的 Promise 拒绝',
    async (reason) => {
      const unhandled = vi.fn();
      window.addEventListener('unhandledrejection', unhandled);

      runHandled(Promise.reject(reason));
      await Promise.resolve();
      await Promise.resolve();

      expect(unhandled).not.toHaveBeenCalled();
      window.removeEventListener('unhandledrejection', unhandled);
    },
  );
});
