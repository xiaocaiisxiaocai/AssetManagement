import { describe, expect, it } from 'vitest';

import { createAsyncSessionTracker } from './async-session-tracker';

describe('异步会话任务隔离', () => {
  it('关闭并重开后，新会话不会等待旧会话任务', () => {
    const tracker = createAsyncSessionTracker();
    const oldToken = tracker.start();
    const oldTask = new Promise<void>(() => {});
    tracker.track(oldTask, oldToken);

    tracker.close();
    const newToken = tracker.start();

    expect(tracker.pending(oldToken)).toEqual([]);
    expect(tracker.pending(newToken)).toEqual([]);
    expect(tracker.hasPending(newToken)).toBe(false);
  });

  it('旧任务完成不会清除新会话的待处理状态', async () => {
    const tracker = createAsyncSessionTracker();
    let finishOld!: () => void;
    const oldTask = new Promise<void>((resolve) => {
      finishOld = resolve;
    });
    tracker.track(oldTask, tracker.start());
    tracker.close();

    let finishNew!: () => void;
    const newTask = new Promise<void>((resolve) => {
      finishNew = resolve;
    });
    const newToken = tracker.start();
    tracker.track(newTask, newToken);

    finishOld();
    await oldTask;
    expect(tracker.hasPending(newToken)).toBe(true);

    finishNew();
    await newTask;
    expect(tracker.hasPending(newToken)).toBe(false);
  });
});
