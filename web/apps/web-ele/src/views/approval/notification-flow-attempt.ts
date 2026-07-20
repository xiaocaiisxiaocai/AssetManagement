export type ApprovalSource = 'asset' | 'material';

export function notificationSource(
  source: unknown,
  path: string,
): ApprovalSource {
  return source === 'material' || path.startsWith('/material/')
    ? 'material'
    : 'asset';
}

export function beginNotificationFlowAttempt(
  flowIdValue: unknown,
  source: ApprovalSource,
  activeSource: ApprovalSource,
  consumedKey: string,
) {
  const flowId = Number(flowIdValue || 0);
  const key = `${source}:${flowId}`;
  const requestedFlowId =
    flowId && source === activeSource && key !== consumedKey
      ? flowId
      : undefined;
  return {
    consumedKey: requestedFlowId ? key : consumedKey,
    key,
    requestedFlowId,
  };
}

export function withoutNotificationFlowId<T extends Record<string, unknown>>(
  query: T,
): Omit<T, 'flowId'> {
  const nextQuery = { ...query };
  delete nextQuery.flowId;
  return nextQuery;
}
