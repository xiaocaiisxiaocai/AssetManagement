import type { SettingValueType } from './settings-form';

export type SettingIntegerRule = { max: number; min: number };

export const booleanSettingKeys = new Set([
  'audit_cleanup_enabled',
  'database_backup_enabled',
  'material.transfer.approval.enabled',
]);

export const timeSettingKeys = new Set([
  'audit_cleanup_time',
  'database_backup_time',
]);

export const integerSettingRules: Record<string, SettingIntegerRule> = {
  attachment_max_mb: { max: 100, min: 1 },
  audit_retention_months: { max: 120, min: 1 },
  database_backup_retention_days: { max: 3650, min: 1 },
  page_size: { max: 200, min: 1 },
};

export const auditRetentionDayOptions = new Set([7, 14, 30]);

export function getValueType(key: string): SettingValueType {
  if (booleanSettingKeys.has(key)) return 'boolean';
  if (timeSettingKeys.has(key)) return 'time';
  if (key === 'audit_retention_days') return 'audit-retention-days';
  if (key === 'asset_condition_options') return 'asset-condition-options';
  if (integerSettingRules[key]) return 'integer';
  return 'text';
}

export function toFormValue(
  key: string,
  value: string,
): number | string | undefined {
  const valueType = getValueType(key);
  if (valueType === 'boolean') {
    return value.toLowerCase() === 'true' ? 'true' : 'false';
  }
  if (valueType === 'integer' || valueType === 'audit-retention-days') {
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) ? parsed : undefined;
  }
  return value;
}

export function toPayloadValue(
  key: string,
  value: number | string | undefined,
  conditionOptions: readonly string[],
): string {
  const valueType = getValueType(key);
  if (valueType === 'asset-condition-options') {
    const options = conditionOptions.map((item) => item.trim());
    if (
      options.length === 0 ||
      options.length > 20 ||
      options.some((item) => !item || item.length > 50) ||
      new Set(options.map((item) => item.toLowerCase())).size !== options.length
    ) {
      throw new Error('请配置 1-20 个不重复的状况选项，每项不超过 50 个字符');
    }
    return JSON.stringify(options);
  }
  if (valueType === 'boolean') {
    if (value !== 'true' && value !== 'false') {
      throw new Error('请选择参数值');
    }
    return value;
  }

  if (valueType === 'time') {
    const text = String(value ?? '').trim();
    if (!/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(text)) {
      throw new Error('请选择正确的时间');
    }
    return text;
  }

  if (valueType === 'audit-retention-days') {
    const numberValue = Number(value);
    if (!auditRetentionDayOptions.has(numberValue)) {
      throw new Error('请选择审计日志保留天数');
    }
    return String(numberValue);
  }

  if (valueType === 'integer') {
    const rule = integerSettingRules[key];
    if (!rule) {
      throw new Error('参数值不合法');
    }
    const numberValue = Number(value);
    if (
      !Number.isInteger(numberValue) ||
      numberValue < rule.min ||
      numberValue > rule.max
    ) {
      throw new Error(`参数值必须是 ${rule.min}-${rule.max} 的整数`);
    }
    return String(numberValue);
  }

  const text = String(value ?? '').trim();
  if (key === 'database_backup_path' && !text) {
    throw new Error('数据库备份目录不能为空');
  }
  if (key.startsWith('category_code_level') && key.endsWith('_length')) {
    const normalized = normalizeLengthRule(text);
    if (normalized === null) {
      throw new Error('编码段位数必须是 1-20 的整数或范围，例如 2 或 2-6');
    }
    return normalized;
  }
  if (key.startsWith('category_code_level') && key.endsWith('_regex')) {
    if (!text) {
      throw new Error('分类编码正则不能为空');
    }
    try {
      new RegExp(text).test('');
    } catch {
      throw new Error('请输入合法正则表达式');
    }
  }
  return text;
}

export function parseStringList(value: string) {
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed)
      ? parsed.filter((item): item is string => typeof item === 'string')
      : [];
  } catch {
    return [];
  }
}

function normalizeLengthRule(value: string) {
  const text = value.trim();
  if (/^\d+$/.test(text)) {
    const exact = Number(text);
    return exact >= 1 && exact <= 20 ? String(exact) : null;
  }
  const match = /^(\d+)\s*-\s*(\d+)$/.exec(text);
  if (!match) return null;
  const min = Number(match[1]);
  const max = Number(match[2]);
  if (
    !Number.isInteger(min) ||
    !Number.isInteger(max) ||
    min < 1 ||
    max > 20 ||
    min > max
  ) {
    return null;
  }
  return `${min}-${max}`;
}
