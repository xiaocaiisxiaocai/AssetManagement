import { describe, expect, it } from 'vitest';

import { isLoginRequestUrl } from './auth-response';

describe('401 响应来源识别', () => {
  it.each([
    '/auth/login',
    '/api/auth/login',
    'http://localhost:5292/api/auth/login?source=web',
  ])('识别登录请求 %s', (url) => {
    expect(isLoginRequestUrl(url)).toBe(true);
  });

  it.each([
    undefined,
    '/auth/user-info',
    '/api/assets',
    '/auth/login-history',
    '/other/auth/login',
  ])('受保护请求不应被当作登录请求 %s', (url) => {
    expect(isLoginRequestUrl(url)).toBe(false);
  });
});
