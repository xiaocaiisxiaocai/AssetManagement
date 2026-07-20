const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})/;
const HAS_TIME_ZONE = /(?:Z|[+-]\d{2}:?\d{2})$/i;
const DEFAULT_TIME_ZONE = 'Asia/Shanghai';

function dateTimeParts(value: Date) {
  return Object.fromEntries(
    new Intl.DateTimeFormat('zh-CN', {
      day: '2-digit',
      hour: '2-digit',
      hour12: false,
      minute: '2-digit',
      month: '2-digit',
      second: '2-digit',
      timeZone: DEFAULT_TIME_ZONE,
      year: 'numeric',
    })
      .formatToParts(value)
      .map((part) => [part.type, part.value]),
  );
}

export function formatDate(value?: Date | null | string, empty = '-') {
  if (!value) return empty;
  if (typeof value === 'string') {
    const match = DATE_PATTERN.exec(value);
    if (match) return `${match[1]}-${match[2]}-${match[3]}`;
  }
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return empty;
  const parts = dateTimeParts(date);
  return `${parts.year}-${parts.month}-${parts.day}`;
}

export function formatDateTime(
  value?: Date | null | string,
  options: { empty?: string; seconds?: boolean } = {},
) {
  const { empty = '-', seconds = false } = options;
  if (!value) return empty;
  let normalized = value;
  if (
    typeof value === 'string' &&
    /[T ]\d{2}:\d{2}/.test(value) &&
    !HAS_TIME_ZONE.test(value)
  ) {
    normalized = `${value.replace(' ', 'T')}Z`;
  }
  const date = normalized instanceof Date ? normalized : new Date(normalized);
  if (Number.isNaN(date.getTime())) return empty;
  const parts = dateTimeParts(date);
  const result = `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}`;
  return seconds ? `${result}:${parts.second}` : result;
}
