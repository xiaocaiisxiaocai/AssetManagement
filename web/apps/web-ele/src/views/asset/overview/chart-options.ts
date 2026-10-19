import type { AssetOverview } from '#/api/asset-overview';

export function buildCategoryDistribution(stats: AssetOverview) {
  const rows = stats.categories.map((item) => ({
    name: item.categoryCode,
    value: item.total,
  }));
  if (stats.uncategorizedTotal > 0)
    rows.push({ name: '未归入二级分类', value: stats.uncategorizedTotal });
  return rows;
}

export function buildCategoryStatusSeries(stats: AssetOverview) {
  return {
    available: stats.categories.map((item) => item.available),
    borrowed: stats.categories.map((item) => item.borrowed),
    labels: stats.categories.map((item) => item.categoryCode),
  };
}

export function categoryShare(total: number, all: number) {
  return all > 0 ? Math.round((total * 10_000) / all) / 100 : 0;
}
