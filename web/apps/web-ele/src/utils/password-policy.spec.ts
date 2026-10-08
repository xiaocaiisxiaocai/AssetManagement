import { describe, expect, it } from 'vitest';

import {
  PASSWORD_RULE_MESSAGE,
  PASSWORD_RULE_PATTERN,
} from './password-policy';

describe('密码规则', () => {
  const pattern = new RegExp(PASSWORD_RULE_PATTERN);

  it.each(['abc12345', 'a1!!!!!!', `a1${'b'.repeat(62)}`])(
    '允许同时包含字母和数字的 8-64 位密码：%s',
    (password) => {
      expect(pattern.test(password)).toBe(true);
    },
  );

  it('拒绝过短、过长、纯字母或纯数字密码', () => {
    expect(pattern.test('1234567')).toBe(false);
    expect(pattern.test('abcdefg')).toBe(false);
    expect(pattern.test('12345678')).toBe(false);
    expect(pattern.test(`a1${'b'.repeat(63)}`)).toBe(false);
  });

  it('提示语描述长度和字符组成', () => {
    expect(PASSWORD_RULE_MESSAGE).toBe(
      '密码须为 8-64 位，且同时包含字母和数字',
    );
  });
});
