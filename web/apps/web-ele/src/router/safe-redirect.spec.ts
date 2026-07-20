import { describe, expect, it } from 'vitest';

import { safeInternalRedirect } from './safe-redirect';

describe('登录回跳地址', () => {
  it('解码站内路径', () => {
    expect(safeInternalRedirect('%2Fasset%2Flist%3Fpage%3D2', '/home')).toBe(
      '/asset/list?page=2',
    );
  });

  it.each([
    '%',
    'https%3A%2F%2Fevil.example',
    '%2F%2Fevil.example',
    '%2F%5Cevil.example',
    '%252F%252Fevil.example',
    '%2F%252F%252Fevil.example',
  ])('非法或外部路径回退到默认页 %s', (value) => {
    expect(safeInternalRedirect(value, '/home')).toBe('/home');
  });
});
