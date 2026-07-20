export function lastPageForTotal(total: number, pageSize: number) {
  return Math.max(1, Math.ceil(Math.max(total, 0) / Math.max(pageSize, 1)));
}

export function normalizedPage(page: number, total: number, pageSize: number) {
  return Math.min(Math.max(page, 1), lastPageForTotal(total, pageSize));
}
