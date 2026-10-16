import { describe, expect, it } from 'vitest';

import {
  deleteFlagsFromStatus,
  deleteStatusFromFlags,
} from './header-multi-filter';

describe('删除状态表头筛选', () => {
  it('全部对应空选择', () => {
    expect(deleteFlagsFromStatus('all')).toEqual([]);
    expect(deleteFlagsFromStatus('active')).toEqual(['active']);
    expect(deleteFlagsFromStatus('deleted')).toEqual(['deleted']);
  });

  it('同时选择或都不选择时回到全部', () => {
    expect(deleteStatusFromFlags([])).toBe('all');
    expect(deleteStatusFromFlags(['active', 'deleted', 0])).toBe('all');
    expect(deleteStatusFromFlags(['active', 1])).toBe('active');
    expect(deleteStatusFromFlags([0, 'deleted'])).toBe('deleted');
  });
});
