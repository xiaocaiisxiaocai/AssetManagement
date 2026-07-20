import { describe, expect, it } from 'vitest';

import { normalizeAssetImageRequestUrl } from './asset';

describe('资产图片地址安全校验', () => {
  it('只接受本系统生成的严格文件地址', () => {
    expect(
      normalizeAssetImageRequestUrl(
        '/api/files/0123456789abcdef0123456789abcdef.png',
      ),
    ).toBe('/files/0123456789abcdef0123456789abcdef.png');
  });

  it.each([
    'https://evil.example/token',
    '//evil.example/token',
    '/api//evil.example/token',
    '/api/files/%2e%2e%2fevil.png',
    '/api/files/0123456789abcdef0123456789abcdef.png?x=1',
    '/api/files/not-a-guid.png',
  ])('拒绝不可信地址 %s', (url) => {
    expect(() => normalizeAssetImageRequestUrl(url)).toThrow('非法的图片地址');
  });
});
