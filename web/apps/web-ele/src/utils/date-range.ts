const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export function startOfSelectedDay(value?: string): string | undefined {
  if (!value) return undefined;
  return DATE_ONLY_PATTERN.test(value) ? `${value}T00:00:00` : value;
}

export function endOfSelectedDay(value?: string): string | undefined {
  if (!value) return undefined;
  return DATE_ONLY_PATTERN.test(value) ? `${value}T23:59:59.999` : value;
}
