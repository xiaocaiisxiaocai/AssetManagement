import { describe, expect, it } from 'vitest';

import { nonempty, serializeRepeatQuery } from './repeat-params';

describe('重复查询参数', () => {
  it('数组使用重复键且不带方括号', () => {
    expect(
      serializeRepeatQuery({
        departmentIds: [2, 8],
        keyword: '相机',
        page: 1,
        status: undefined,
        statuses: [0],
      }),
    ).toBe(
      'departmentIds=2&departmentIds=8&keyword=%E7%9B%B8%E6%9C%BA&page=1&statuses=0',
    );
  });

  it('空数组不出现在查询串中', () => {
    expect(serializeRepeatQuery({ departmentIds: [], page: 1 })).toBe('page=1');
    expect(nonempty([])).toBeUndefined();
    expect(nonempty([3])).toEqual([3]);
  });
});
