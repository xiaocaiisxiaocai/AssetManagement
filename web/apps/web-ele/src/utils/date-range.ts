const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const CHINA_TIME_ZONE_OFFSET = '+08:00';

function chinaDayBoundaryToUtc(value: string, time: string) {
  return new Date(`${value}T${time}${CHINA_TIME_ZONE_OFFSET}`).toISOString();
}

export function startOfSelectedDay(value?: string): string | undefined {
  if (!value) return undefined;
  return DATE_ONLY_PATTERN.test(value)
    ? chinaDayBoundaryToUtc(value, '00:00:00.000')
    : value;
}

export function endOfSelectedDay(value?: string): string | undefined {
  if (!value) return undefined;
  return DATE_ONLY_PATTERN.test(value)
    ? chinaDayBoundaryToUtc(value, '23:59:59.999')
    : value;
}
