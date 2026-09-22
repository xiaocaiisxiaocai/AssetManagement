import { describe, expect, it } from 'vitest';

import { authenticatedRootRedirect } from './home-redirect';

describe('已鉴权根路由跳转', () => {
  it('根路径跳转到用户首页或默认首页', () => {
    expect(authenticatedRootRedirect('/', '/workspace')).toBe('/workspace');
    expect(authenticatedRootRedirect('/', '')).toBe('/home');
  });

  it('不改写正常业务路径和未知路径', () => {
    expect(authenticatedRootRedirect('/home', '/workspace')).toBeNull();
    expect(
      authenticatedRootRedirect('/e2e-page-does-not-exist', '/workspace'),
    ).toBeNull();
  });
});
