export interface SettledSnapshot<T> {
  error: null | string;
  value: null | T;
}

function resolveErrorMessage(reason: unknown) {
  if (reason instanceof Error && reason.message.trim()) return reason.message;
  return '加载失败';
}

export function mergeSettledValue<T>(
  current: null | T,
  result: PromiseSettledResult<null | T>,
): SettledSnapshot<T> {
  if (result.status === 'fulfilled') {
    return { error: null, value: result.value };
  }

  return {
    error: resolveErrorMessage(result.reason),
    value: current,
  };
}
