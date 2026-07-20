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
