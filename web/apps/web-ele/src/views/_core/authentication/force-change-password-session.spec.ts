import type { UserInfo } from '@vben/types';

import { describe, expect, it } from 'vitest';

import {
  formatForceChangePasswordAccount,
  resolveForceChangePasswordTarget,
} from './force-change-password-session';

describe('强制修改初始密码会话判断', () => {
  it('未取得用户信息时回登录页', () => {
    expect(resolveForceChangePasswordTarget(null)).toBe('/auth/login');
  });

  it('账号不需要修改初始密码时回首页', () => {
    expect(
      resolveForceChangePasswordTarget({
        homePath: '/asset/list',
        mustChangePassword: false,
      } as UserInfo & { mustChangePassword: boolean }),
    ).toBe('/asset/list');
  });

  it('账号仍需修改初始密码时留在当前页', () => {
    expect(
      resolveForceChangePasswordTarget({
        mustChangePassword: true,
      } as UserInfo & { mustChangePassword: boolean }),
    ).toBeNull();
  });

  it('显示当前账号和姓名，避免用户误以为系统未知账号', () => {
    expect(
      formatForceChangePasswordAccount({
        realName: '系统管理员',
        username: '1001',
      } as UserInfo),
    ).toBe('1001 / 系统管理员');
  });
});
