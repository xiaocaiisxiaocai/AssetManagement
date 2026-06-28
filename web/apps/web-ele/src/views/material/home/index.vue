<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';

import { getTestProjectStatsApi, type TestProjectStats } from '#/api/test-project';

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

const MONTHS = ['1月','2月','3月','4月','5月','6月','7月','8月','9月','10月','11月','12月'];

function renderCharts(data: TestProjectStats) {
  // 类型分布饼图
  renderTypeChart({
    title: { text: '测评类型分布', left: 'center', top: 8, textStyle: { fontSize: 14 } },
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', right: 16, top: 'middle' },
    series: [{
      type: 'pie',
      radius: ['38%', '62%'],
      center: ['40%', '55%'],
      avoidLabelOverlap: false,
      label: {
        show: true,
        formatter: '{b}\n{c}; {d}%',
        fontSize: 12,
      },
      data: data.typeDist.map(x => ({ name: x.label, value: x.count })),
      color: ['#1890ff', '#fa8c16', '#13c2c2', '#722ed1'],
    }],
  });

  // 状态分布饼图
  renderStatusChart({
    title: { text: '进度状态分布', left: 'center', top: 8, textStyle: { fontSize: 14 } },
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', right: 16, top: 'middle' },
    series: [{
      type: 'pie',
      radius: ['38%', '62%'],
      center: ['40%', '55%'],
      avoidLabelOverlap: false,
      label: {
        show: true,
        formatter: '{b}\n{c}; {d}%',
        fontSize: 12,
      },
      data: [
        { name: '进行中', value: data.inProgress, itemStyle: { color: '#1890ff' } },
        { name: '结案', value: data.closed, itemStyle: { color: '#7b68ee' } },
        { name: '落地跟进', value: data.landed, itemStyle: { color: '#13c2c2' } },
      ].filter(x => x.value > 0),
    }],
  });

  // 柱线组合图
  const closedData = data.monthlyStat.map(x => x.closedCount);
  const landedData = data.monthlyStat.map(x => x.landedCount);
  renderBarChart({
    title: { text: '结案与落地数据统计', left: 'center', top: 8, textStyle: { fontSize: 14 } },
    tooltip: { trigger: 'axis' },
    legend: { bottom: 0, data: ['结案数量', '落地数量'] },
    grid: { top: 50, left: 40, right: 20, bottom: 50 },
    xAxis: { type: 'category', data: MONTHS },
    yAxis: { type: 'value', minInterval: 1 },
    series: [
      {
        name: '结案数量',
        type: 'bar',
        data: closedData,
        itemStyle: { color: '#1890ff' },
        label: { show: true, position: 'top', fontSize: 11,
          formatter: (p: any) => p.value > 0 ? String(p.value) : '' },
      },
      {
        name: '落地数量',
        type: 'line',
        data: landedData,
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
  renderCharts(data);
});
</script>

<template>
  <div class="p-4 space-y-4">
    <!-- 顶部统计卡片 -->
    <div class="grid grid-cols-4 gap-0 border border-gray-200 rounded overflow-hidden">
      <div class="stat-card border-r border-gray-200 border-l-4 border-l-blue-500">
        <div class="stat-num text-blue-500">{{ stats.total }}</div>
        <div class="stat-label">总测评数</div>
      </div>
      <div class="stat-card border-r border-gray-200 border-l-4 border-l-green-500">
        <div class="stat-num text-green-500">{{ stats.closed }}</div>
        <div class="stat-label">已结案</div>
      </div>
      <div class="stat-card border-r border-gray-200 border-l-4 border-l-purple-500">
        <div class="stat-num text-purple-500">{{ stats.inProgress }}</div>
        <div class="stat-label">进行中</div>
      </div>
      <div class="stat-card border-l-4 border-l-red-500">
        <div class="stat-num text-red-500">{{ stats.landed }}</div>
        <div class="stat-label">已落地</div>
      </div>
    </div>

    <!-- 中间两个饼图 -->
    <div class="grid grid-cols-2 gap-4">
      <div class="chart-card">
        <EchartsUI ref="typeChartRef" style="height: 260px;" />
      </div>
      <div class="chart-card">
        <EchartsUI ref="statusChartRef" style="height: 260px;" />
      </div>
    </div>

    <!-- 底部柱线组合图 -->
    <div class="chart-card">
      <EchartsUI ref="barChartRef" style="height: 280px;" />
    </div>
  </div>
</template>

<style scoped>
.stat-card {
  @apply flex flex-col justify-center px-8 py-6 bg-white;
}
.stat-num {
  @apply text-5xl font-bold leading-none mb-2;
}
.stat-label {
  @apply text-sm text-gray-500 mt-1;
}
.chart-card {
  @apply bg-white border border-gray-200 rounded p-3;
}
</style>

