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
    maintenance: stats.categories.map((item) => item.maintenance),
    scrapped: stats.categories.map((item) => item.scrapped),
    labels: stats.categories.map((item) => item.categoryCode),
  };
}
