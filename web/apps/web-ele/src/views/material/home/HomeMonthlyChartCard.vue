<script lang="ts" setup>
import type { TestProjectStats } from '#/api/test-project';

import { ref } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';

import {
  buildMonthlySeriesData,
  monthLabels,
  quantityAxisLabel,
} from './chart-options';
import { getChartTheme } from './home-chart-theme';
import { useHomeChartCard } from './use-home-chart-card';

const props = defineProps<{
  loading: boolean;
  stats: null | TestProjectStats;
}>();

const barChartRef = ref<EchartsUIType>();
const { renderEcharts: renderBarChart } = useEcharts(barChartRef);

function renderChart(data: TestProjectStats) {
  const theme = getChartTheme();
  const titleTextStyle = { color: theme.text, fontSize: 14, fontWeight: 600 };
  const legendTextStyle = { color: theme.muted };
  const axisLabel = { color: theme.muted };
  const splitLine = { lineStyle: { color: theme.gridLine } };
  const axisLine = { lineStyle: { color: theme.axisLine } };
  const { closedData, followUpData } = buildMonthlySeriesData(data.monthlyStat);
  renderBarChart({
    aria: {
      enabled: true,
      decal: { show: true },
      label: { description: `${data.year}年结案数量与跟进记录数量统计图` },
    },
    backgroundColor: 'transparent',
    title: {
      text: `${data.year}年结案与跟进记录统计`,
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
          formatter: (parameter) =>
            Number(parameter.value) > 0 ? String(parameter.value) : '',
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

useHomeChartCard(() => props.stats, renderChart);
</script>

<template>
  <div class="chart-card" v-loading="loading">
    <div class="chart-canvas">
      <EchartsUI
        ref="barChartRef"
        aria-label="全年结案与跟进记录统计图"
        class="chart-plot"
        height="100%"
        role="img"
      />
    </div>
  </div>
</template>

<style scoped src="./HomeCharts.css"></style>
