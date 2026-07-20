import { describe, expect, it, vi } from 'vitest';

import { createSingleFlight } from './single-flight';

describe('single flight', () => {
  it('并发调用共用同一个执行任务', async () => {
    let resolve!: (value: number) => void;
    const task = vi.fn(
      () =>
        new Promise<number>((done) => {
          resolve = done;
        }),
    );
    const run = createSingleFlight(task);
    const requests = [run(), run(), run(), run(), run()];

    expect(task).toHaveBeenCalledTimes(1);
    resolve(7);
    await expect(Promise.all(requests)).resolves.toEqual([7, 7, 7, 7, 7]);
  });

  it('上一次完成后允许再次执行', async () => {
    const task = vi.fn(async () => 1);
    const run = createSingleFlight(task);
    await run();
    await run();
    expect(task).toHaveBeenCalledTimes(2);
  });
});
