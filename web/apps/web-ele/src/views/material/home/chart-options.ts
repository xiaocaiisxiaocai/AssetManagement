import type { TestProjectStats } from '#/api/test-project';

export type MonthlyStat = TestProjectStats['monthlyStat'][number];

export const monthLabels = Array.from({ length: 12 }, (_, index) => `${index + 1}月`);

export const quantityAxisLabel = {
  formatter: (value: number) => `${value}个`,
};

export function buildMonthlySeriesData(monthlyStat: MonthlyStat[]) {
  const byMonth = new Map(
    monthlyStat
      .filter((item) => item.month >= 1 && item.month <= 12)
      .map((item) => [item.month, item]),
  );

  return {
    closedData: Array.from({ length: 12 }, (_, index) => {
      return byMonth.get(index + 1)?.closedCount ?? 0;
    }),
    landedData: Array.from({ length: 12 }, (_, index) => {
      return byMonth.get(index + 1)?.landedCount ?? 0;
    }),
  };
}
