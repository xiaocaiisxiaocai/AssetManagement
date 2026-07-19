<script lang="ts" setup>
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';

import {
  getTestProjectStatsApi,
  type TestProjectStats,
} from '#/api/test-project';

import {
  buildMonthlySeriesData,
  monthLabels,
  quantityAxisLabel,
} from './chart-options';

const stats = ref<TestProjectStats>({
  total: 0,
  closed: 0,
  inProgress: 0,
  landed: 0,
  typeDist: [],
  monthlyStat: [],
});

// 饼图：测评类型分布
const typeChartRef = ref<EchartsUIType>();
const { renderEcharts: renderTypeChart } = useEcharts(typeChartRef);

// 饼图：进度状态分布
const statusChartRef = ref<EchartsUIType>();
const { renderEcharts: renderStatusChart } = useEcharts(statusChartRef);

// 柱线组合图：结案与落地数据统计
const barChartRef = ref<EchartsUIType>();
const { renderEcharts: renderBarChart } = useEcharts(barChartRef);

let themeObserver: MutationObserver | undefined;

function readCssColor(variableName: string, fallback: string) {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(variableName)
    .trim();
  return value ? `hsl(${value})` : fallback;
}

function getChartTheme() {
  const text = readCssColor('--foreground', '#1f2937');
  const muted = readCssColor('--muted-foreground', '#6b7280');
  const border = readCssColor('--border', '#e5e7eb');
  return {
    axisLine: border,
    gridLine: border,
    muted,
    text,
  };
}

function renderCharts(data: TestProjectStats) {
  const theme = getChartTheme();
  const titleTextStyle = { color: theme.text, fontSize: 14, fontWeight: 600 };
  const legendTextStyle = { color: theme.muted };
  const axisLabel = { color: theme.muted };
  const splitLine = { lineStyle: { color: theme.gridLine } };
  const axisLine = { lineStyle: { color: theme.axisLine } };

  // 类型分布饼图
  renderTypeChart({
    backgroundColor: 'transparent',
    title: {
      text: '测评类型分布',
      left: 'center',
      top: 8,
      textStyle: titleTextStyle,
    },
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: {
      orient: 'vertical',
      right: 16,
      top: 'middle',
      textStyle: legendTextStyle,
    },
    series: [
      {
        type: 'pie',
        radius: ['38%', '62%'],
        center: ['40%', '55%'],
        avoidLabelOverlap: false,
        label: {
          show: true,
          formatter: '{b}\n{c}; {d}%',
          fontSize: 12,
          color: theme.muted,
        },
        data: data.typeDist.map((x) => ({ name: x.label, value: x.count })),
        color: ['#1890ff', '#fa8c16', '#13c2c2', '#722ed1'],
      },
    ],
  });

  // 状态分布饼图
  renderStatusChart({
    backgroundColor: 'transparent',
    title: {
      text: '进度状态分布',
      left: 'center',
      top: 8,
      textStyle: titleTextStyle,
    },
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: {
      orient: 'vertical',
      right: 16,
      top: 'middle',
      textStyle: legendTextStyle,
    },
    series: [
      {
        type: 'pie',
        radius: ['38%', '62%'],
        center: ['40%', '55%'],
        avoidLabelOverlap: false,
        label: {
          show: true,
          formatter: '{b}\n{c}; {d}%',
          fontSize: 12,
          color: theme.muted,
        },
        data: [
          {
            name: '计划/测试中',
            value: data.inProgress,
            itemStyle: { color: '#1890ff' },
          },
          { name: '结案', value: data.closed, itemStyle: { color: '#7b68ee' } },
          {
            name: '落地跟进',
            value: data.landed,
            itemStyle: { color: '#13c2c2' },
          },
        ].filter((x) => x.value > 0),
      },
    ],
  });

  // 柱线组合图
  const { closedData, followUpData } = buildMonthlySeriesData(data.monthlyStat);
  renderBarChart({
    backgroundColor: 'transparent',
    title: {
      text: '结案与跟进记录统计',
      left: 'center',
      top: 8,
      textStyle: titleTextStyle,
    },
    tooltip: { trigger: 'axis' },
    legend: {
      bottom: 0,
      data: ['结案数量', '跟进记录数'],
      textStyle: legendTextStyle,
    },
    grid: { top: 50, left: 56, right: 20, bottom: 50 },
    xAxis: { type: 'category', data: monthLabels, axisLabel, axisLine },
    yAxis: {
      type: 'value',
      minInterval: 1,
      axisLabel: { ...axisLabel, ...quantityAxisLabel },
      splitLine,
    },
    series: [
      {
        name: '结案数量',
        type: 'bar',
        data: closedData,
        itemStyle: { color: '#1890ff' },
        label: {
          show: true,
          position: 'top',
          fontSize: 11,
          color: theme.muted,
          formatter: (p: any) => (p.value > 0 ? String(p.value) : ''),
        },
      },
      {
        name: '跟进记录数',
        type: 'line',
        data: followUpData,
        itemStyle: { color: '#7b68ee' },
        symbol: 'circle',
        symbolSize: 6,
      },
    ],
  });
}

onMounted(async () => {
  const data = await getTestProjectStatsApi();
  stats.value = data;
  await nextTick();
  renderCharts(data);
  themeObserver = new MutationObserver(() => renderCharts(stats.value));
  themeObserver.observe(document.documentElement, {
    attributeFilter: ['class', 'data-theme', 'style'],
    attributes: true,
  });
});

onBeforeUnmount(() => {
  themeObserver?.disconnect();
});
</script>

<template>
  <re-page>
    <div class="material-home-page p-4">
      <!-- 顶部统计卡片 -->
      <div class="summary-grid">
        <div class="summary-card summary-card-blue">
          <div class="stat-num text-blue-500">{{ stats.total }}</div>
          <div class="stat-label">总测评数</div>
        </div>
        <div class="summary-card summary-card-green">
          <div class="stat-num text-green-500">{{ stats.closed }}</div>
          <div class="stat-label">已结案</div>
        </div>
        <div class="summary-card summary-card-purple">
          <div class="stat-num text-purple-500">{{ stats.inProgress }}</div>
          <div class="stat-label">计划/测试中</div>
        </div>
        <div class="summary-card summary-card-red">
          <div class="stat-num text-red-500">{{ stats.landed }}</div>
          <div class="stat-label">落地跟进</div>
        </div>
      </div>

      <!-- 中间两个饼图 -->
      <div class="grid grid-cols-2 gap-4">
        <div class="chart-card">
          <EchartsUI ref="typeChartRef" style="height: 260px" />
        </div>
        <div class="chart-card">
          <EchartsUI ref="statusChartRef" style="height: 260px" />
        </div>
      </div>

      <!-- 底部柱线组合图 -->
      <div class="chart-card">
        <EchartsUI ref="barChartRef" style="height: 280px" />
      </div>
    </div>
  </re-page>
</template>

<style scoped>
.material-home-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  box-sizing: border-box;
  height: calc(var(--vben-content-height, 100vh) - 32px);
  min-height: 0;
  overflow: hidden;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  flex-shrink: 0;
}

.summary-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 118px;
  padding: 24px 32px;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  text-align: center;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}
.stat-num {
  @apply mb-2 text-5xl font-bold leading-none;
}
.stat-label {
  margin-top: 4px;
  color: var(--asset-page-muted);
  font-size: 14px;
  line-height: 20px;
}
.chart-card {
  min-height: 0;
  padding: 12px;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

@media (max-width: 1024px) {
  .material-home-page {
    height: auto;
    min-height: 100%;
    overflow-y: auto;
  }

  .summary-grid,
  .grid-cols-2 {
    grid-template-columns: 1fr;
  }

  .summary-card {
    min-height: 96px;
  }
}
</style>
