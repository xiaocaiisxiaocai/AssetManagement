import { describe, expect, it, vi } from 'vitest';

import { createNotificationSyncGuard } from './notification-sync-guard';

describe('通知轮询与变更同步', () => {
  it('通知变更入队后立即拒绝旧轮询响应写回', async () => {
    const guard = createNotificationSyncGuard();
    const refresh = guard.beginRefresh();
    let finish!: () => void;

    const mutation = guard.enqueueMutation(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    expect(guard.canCommitRefresh(refresh)).toBe(false);
    expect(guard.beginRefresh()).toBeNull();
    await Promise.resolve();
    finish();
    await mutation;
  });

  it('变更完成后允许新轮询，但仍拒绝变更前响应', async () => {
    const guard = createNotificationSyncGuard();
    const staleRefresh = guard.beginRefresh();
    await guard.enqueueMutation(async () => {});
    const latestRefresh = guard.beginRefresh();

    expect(guard.canCommitRefresh(staleRefresh)).toBe(false);
    expect(guard.canCommitRefresh(latestRefresh)).toBe(true);
  });

  it('按入队顺序执行连续的已读和清空操作', async () => {
    const guard = createNotificationSyncGuard();
    const order: string[] = [];
    let finishFirst!: () => void;
    const first = guard.enqueueMutation(async () => {
      order.push('mark-read:start');
      await new Promise<void>((resolve) => {
        finishFirst = resolve;
      });
      order.push('mark-read:end');
    });
    const secondTask = vi.fn(async () => {
      order.push('clear');
    });
    const second = guard.enqueueMutation(secondTask);

    await Promise.resolve();
    expect(secondTask).not.toHaveBeenCalled();
    finishFirst();
    await Promise.all([first, second]);
    expect(order).toEqual(['mark-read:start', 'mark-read:end', 'clear']);
  });

  it('前一个变更失败也不会阻断后续队列', async () => {
    const guard = createNotificationSyncGuard();
    const next = vi.fn(async () => 'done');
    const failed = guard.enqueueMutation(async () => {
      throw new Error('mark read failed');
    });
    const succeeded = guard.enqueueMutation(next);

    await expect(failed).rejects.toThrow('mark read failed');
    await expect(succeeded).resolves.toBe('done');
    expect(next).toHaveBeenCalledOnce();
  });
});
