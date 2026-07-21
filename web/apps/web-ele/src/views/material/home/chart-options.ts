import type { TestProjectStats } from '#/api/test-project';

export type MonthlyStat = TestProjectStats['monthlyStat'][number];

export const monthLabels = Array.from(
  { length: 12 },
  (_, index) => `${index + 1}月`,
);

export const quantityAxisLabel = {
  formatter: (value: number) => `${value}个`,
};

export function buildStatusDistribution(
  stats: Pick<TestProjectStats, 'closed' | 'inProgress' | 'landed'>,
) {
  return [
    { label: '计划/测试中', value: stats.inProgress },
    { label: '结案', value: stats.closed },
    { label: '落地跟进', value: stats.landed },
  ];
}

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
    followUpData: Array.from({ length: 12 }, (_, index) => {
      return byMonth.get(index + 1)?.followUpCount ?? 0;
    }),
  };
}

export function buildMonthlyTableRows(monthlyStat: MonthlyStat[]) {
  const { closedData, followUpData } = buildMonthlySeriesData(monthlyStat);
  return monthLabels.map((month, index) => ({
    closedCount: closedData[index] ?? 0,
    followUpCount: followUpData[index] ?? 0,
    month,
  }));
}
