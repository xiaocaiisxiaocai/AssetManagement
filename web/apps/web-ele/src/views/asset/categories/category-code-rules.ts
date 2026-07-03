export interface CategoryCodeRule {
  length: string;
  regex: string;
}

export interface CategoryCodeRules {
  level1: CategoryCodeRule;
  level2: CategoryCodeRule;
  level3: CategoryCodeRule;
}

export const defaultCategoryCodeRules: CategoryCodeRules = {
  level1: { length: '2-6', regex: '^[A-Za-z0-9]+$' },
  level2: { length: '2-6', regex: '^[A-Za-z0-9]+$' },
  level3: { length: '2-6', regex: '^[A-Za-z0-9]+$' },
};

export function categoryCodeRuleHint(level: number, rules: CategoryCodeRules) {
  const rule = getRule(level, rules);
  return `当前为${levelName(level)}分类，编码段要求：${rule.length} 位，可输入字母和数字`;
}

export function validateCategoryCodeSeg(
  codeSeg: string,
  level: number,
  rules: CategoryCodeRules,
) {
  const value = codeSeg.trim();
  if (!value) return '请填写编码段';

  const rule = getRule(level, rules);
  const lengthRule = parseLengthRule(rule.length);
  if (!lengthRule) return '分类编码长度规则配置错误，请检查系统参数';

  const name = levelName(level);
  if (value.length < lengthRule.min || value.length > lengthRule.max) {
    return `${name}分类编码段必须是 ${rule.length} 位`;
  }

  try {
    if (!new RegExp(rule.regex).test(value)) {
      return `${name}分类编码段格式不正确，应匹配 ${rule.regex}`;
    }
  } catch {
    return '分类编码规则配置错误，请检查系统参数';
  }

  return null;
}

function getRule(level: number, rules: CategoryCodeRules) {
  if (level === 1) return rules.level1;
  if (level === 2) return rules.level2;
  return rules.level3;
}

function levelName(level: number) {
  if (level === 1) return '一级';
  if (level === 2) return '二级';
  if (level === 3) return '三级';
  return `${level}级`;
}

function parseLengthRule(raw: string) {
  const text = raw.trim();
  if (/^\d+$/.test(text)) {
    const value = Number(text);
    return value >= 1 && value <= 20 ? { max: value, min: value } : null;
  }

  const match = /^(\d+)\s*-\s*(\d+)$/.exec(text);
  if (!match) return null;

  const min = Number(match[1]);
  const max = Number(match[2]);
  if (!Number.isInteger(min) || !Number.isInteger(max) || min < 1 || max > 20 || min > max) {
    return null;
  }

  return { max, min };
}
