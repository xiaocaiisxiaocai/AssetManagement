import { describe, expect, it } from 'vitest';

import {
  PASSWORD_RULE_MESSAGE,
  PASSWORD_RULE_PATTERN,
} from './password-policy';

describe('密码长度规则', () => {
  const pattern = new RegExp(PASSWORD_RULE_PATTERN);

  it.each(['abcdef', '654321', '!!!!!!', '中中中中中中'])(
    '允许不限制字符组成的六位密码：%s',
    (password) => {
      expect(pattern.test(password)).toBe(true);
    },
  );

  it('拒绝少于六位或超过 12 位的密码', () => {
    expect(pattern.test('12345')).toBe(false);
    expect(pattern.test('a'.repeat(13))).toBe(false);
  });

  it('提示语只描述长度要求', () => {
    expect(PASSWORD_RULE_MESSAGE).toBe('请输入 6-12 位密码');
  });
});
