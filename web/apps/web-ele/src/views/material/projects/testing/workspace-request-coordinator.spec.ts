import { describe, expect, it, vi } from 'vitest';

import { createWorkspaceRequestCoordinator } from './workspace-request-coordinator';

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, reject, resolve };
}

describe('测试工作台请求协调器', () => {
  it('项目切换后只允许新项目响应写回', async () => {
    const coordinator = createWorkspaceRequestCoordinator();
    const first = deferred<string>();
    const second = deferred<string>();
    const onSuccess = vi.fn();

    const firstRun = coordinator.load({
      enabled: true,
      onError: vi.fn(),
      onSettled: vi.fn(),
      onStart: vi.fn(),
      onSuccess,
      projectId: 1,
      request: () => first.promise,
    });
    const secondRun = coordinator.load({
      enabled: true,
      onError: vi.fn(),
      onSettled: vi.fn(),
      onStart: vi.fn(),
      onSuccess,
      projectId: 2,
      request: () => second.promise,
    });

    second.resolve('项目二');
    await secondRun;
    first.resolve('项目一');
    await firstRun;

    expect(onSuccess).toHaveBeenCalledOnce();
    expect(onSuccess).toHaveBeenCalledWith('项目二');
  });

  it('抽屉关闭后未完成响应不会写回或改变加载状态', async () => {
    const coordinator = createWorkspaceRequestCoordinator();
    const pending = deferred<string>();
    const onSuccess = vi.fn();
    const onSettled = vi.fn();
    const run = coordinator.load({
      enabled: true,
      onError: vi.fn(),
      onSettled,
      onStart: vi.fn(),
      onSuccess,
      projectId: 8,
      request: () => pending.promise,
    });

    coordinator.invalidate();
    pending.resolve('旧响应');
    await run;

    expect(onSuccess).not.toHaveBeenCalled();
    expect(onSettled).not.toHaveBeenCalled();
  });

  it('仅将当前请求错误交给页面错误态', async () => {
    const coordinator = createWorkspaceRequestCoordinator();
    const onError = vi.fn();
    const onSettled = vi.fn();

    await coordinator.load({
      enabled: true,
      onError,
      onSettled,
      onStart: vi.fn(),
      onSuccess: vi.fn(),
      projectId: 3,
      request: async () => {
        throw new Error('服务不可用');
      },
    });

    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]?.[0]).toEqual(new Error('服务不可用'));
    expect(onSettled).toHaveBeenCalledOnce();
  });
});
