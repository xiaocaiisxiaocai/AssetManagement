import { describe, expect, it } from 'vitest';

import {
  categoryCodeRuleHint,
  validateCategoryCodeSeg,
} from './category-code-rules';

const rules = {
  level1: { length: '2-4', regex: '^[A-Za-z]{2,4}$' },
  level2: { length: '3-5', regex: '^[0-9]{3,5}$' },
  level3: { length: '2-6', regex: '^[A-Za-z0-9]{2,6}$' },
};

describe('资产分类编码规则', () => {
  it('按层级生成提示文案', () => {
    expect(categoryCodeRuleHint(2, rules)).toBe(
      '当前为二级分类，编码段要求：3-5 位，可输入字母和数字',
    );
  });

  it('按层级校验长度和正则', () => {
    expect(validateCategoryCodeSeg('', 1, rules)).toBe('请填写编码段');
    expect(validateCategoryCodeSeg('A', 1, rules)).toBe('一级分类编码段必须是 2-4 位');
    expect(validateCategoryCodeSeg('ABCDE', 1, rules)).toBe(
      '一级分类编码段必须是 2-4 位',
    );
    expect(validateCategoryCodeSeg('12', 1, rules)).toBe(
      '一级分类编码段格式不正确，应匹配 ^[A-Za-z]{2,4}$',
    );
    expect(validateCategoryCodeSeg('12345', 2, rules)).toBeNull();
  });
});
