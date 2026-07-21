export function approvalDashboardCounts(
  assetPending: number,
  materialPending: number,
  assetMinePending: number,
  materialMinePending: number,
) {
  return {
    minePending: assetMinePending + materialMinePending,
    pending: assetPending + materialPending,
  };
}

export function combineAvailableCounts(
  sources: { enabled: boolean; value: null | number }[],
) {
  const enabledSources = sources.filter((source) => source.enabled);
  if (enabledSources.some((source) => source.value === null)) return null;
  return enabledSources.reduce(
    (total, source) => total + (source.value ?? 0),
    0,
  );
}
