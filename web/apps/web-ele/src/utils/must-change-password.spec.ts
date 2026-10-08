import { describe, expect, it } from 'vitest';

import {
  passwordChangeRedirect,
  tokenRequiresPasswordChange,
} from './must-change-password';

function token(payload: Record<string, unknown>) {
  const json = JSON.stringify(payload);
  const body = btoa(json)
    .replace(/=+$/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
  return `header.${body}.signature`;
}

describe('默认密码改密门禁', () => {
  it('只认必须改密标记', () => {
    expect(tokenRequiresPasswordChange(token({ mustChangePassword: 'true' }))).toBe(
      true,
    );
    expect(tokenRequiresPasswordChange(token({ mustChangePassword: true }))).toBe(
      true,
    );
    expect(tokenRequiresPasswordChange(token({}))).toBe(false);
    expect(tokenRequiresPasswordChange(null)).toBe(false);
    expect(tokenRequiresPasswordChange('not-a-jwt')).toBe(false);
  });

  it('刷新后仍留在登录页，其他地址回到登录页', () => {
    const forced = token({ mustChangePassword: 'true' });
    expect(passwordChangeRedirect('/auth/login', forced)).toBeNull();
    expect(passwordChangeRedirect('/asset/list', forced)).toBe('/auth/login');
    expect(passwordChangeRedirect('/', forced)).toBe('/auth/login');
    expect(passwordChangeRedirect('/asset/list', token({}))).toBeNull();
  });
});
