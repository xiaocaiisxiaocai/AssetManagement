import { describe, expect, it } from 'vitest';

import { normalizedPage } from './pagination';

describe('服务端分页页码回退', () => {
  it('删除末页最后一条后回退到新的末页', () => {
    expect(normalizedPage(3, 20, 10)).toBe(2);
  });

  it('空列表仍使用第一页', () => {
    expect(normalizedPage(4, 0, 10)).toBe(1);
  });
});
