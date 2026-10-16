/** 把数组序列化成重复键，供 ASP.NET 绑定 int[] / string[]。 */
export function serializeRepeatQuery(
  params: Record<string, unknown>,
): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) {
      for (const item of value) {
        if (item === undefined || item === null || item === '') continue;
        search.append(key, String(item));
      }
      continue;
    }
    search.append(key, String(value));
  }
  return search.toString();
}

export const repeatArrayParams = {
  paramsSerializer: { serialize: serializeRepeatQuery },
};

export function nonempty<T>(values: readonly T[] | undefined): T[] | undefined {
  return values && values.length > 0 ? [...values] : undefined;
}
