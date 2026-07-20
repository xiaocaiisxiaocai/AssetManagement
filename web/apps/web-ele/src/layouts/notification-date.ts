const HAS_TIME_ZONE = /(?:Z|[+-]\d{2}:?\d{2})$/i;

/** 后端 DateTime(UTC) 在 JSON 中可能不带 Z，统一按 UTC 解析。 */
export function parseBackendUtcDateTime(value: string) {
  const normalized =
    /[T ]\d{2}:\d{2}/.test(value) && !HAS_TIME_ZONE.test(value)
      ? `${value.replace(' ', 'T')}Z`
      : value;
  return new Date(normalized);
}

export function formatNotificationDate(value: string, now = new Date()) {
  const date = parseBackendUtcDateTime(value);
  if (Number.isNaN(date.getTime())) return '-';
  const diff = Math.max(0, Math.floor((now.getTime() - date.getTime()) / 1000));
  if (diff < 60) return '刚刚';
  if (diff < 3600) return `${Math.floor(diff / 60)}分钟前`;
  if (diff < 86_400) return `${Math.floor(diff / 3600)}小时前`;
  return date.toLocaleDateString('zh-CN', { timeZone: 'Asia/Shanghai' });
}
