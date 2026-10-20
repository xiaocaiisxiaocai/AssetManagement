<script lang="ts" setup>
import type { TestProjectStats } from '#/api/test-project';

import { computed, ref } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';

import {
  buildMonthlySeriesData,
  buildMonthlyTableRows,
  monthLabels,
  quantityAxisLabel,
} from './chart-options';
import { getChartTheme } from './home-chart-theme';
import { useHomeChartCard } from './use-home-chart-card';

const props = defineProps<{
  loading: boolean;
  stats: null | TestProjectStats;
}>();

const monthlyTableRows = computed(() =>
  props.stats ? buildMonthlyTableRows(props.stats.monthlyStat) : [],
);

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
  <div class="chart-card page-fill" v-loading="loading">
    <EchartsUI
      ref="barChartRef"
      aria-label="全年结案与跟进记录统计图"
      role="img"
      style="height: 280px"
    />
    <details class="chart-data-details">
      <summary>
        查看{{ stats?.year ? stats.year + '年' : '' }}结案与跟进数据表
      </summary>
      <div class="chart-data-table-wrap">
        <table class="chart-data-table">
          <caption>
            {{
              stats?.year ? stats.year + '年' : ''
            }}结案与跟进记录统计
          </caption>
          <thead>
            <tr>
              <th scope="col">月份</th>
              <th scope="col">结案数量</th>
              <th scope="col">跟进记录数</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in monthlyTableRows" :key="item.month">
              <th scope="row">{{ item.month }}</th>
              <td>{{ item.closedCount }}</td>
              <td>{{ item.followUpCount }}</td>
            </tr>
            <tr v-if="monthlyTableRows.length === 0">
              <td colspan="3">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </details>
  </div>
</template>

<style scoped src="./HomeCharts.css"></style>
