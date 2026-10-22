<script lang="ts" setup>
import type { TestProjectStats } from '#/api/test-project';

import { computed, ref } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';

import { ElEmpty } from 'element-plus';

import { buildStatusDistribution } from './chart-options';
import { getChartTheme } from './home-chart-theme';
import { useHomeChartCard } from './use-home-chart-card';

const props = defineProps<{
  loading: boolean;
  stats: null | TestProjectStats;
}>();

const statusDistribution = computed(() =>
  props.stats ? buildStatusDistribution(props.stats) : [],
);
const hasData = computed(() =>
  statusDistribution.value.some((item) => item.value > 0),
);

const statusChartRef = ref<EchartsUIType>();
const { renderEcharts: renderStatusChart } = useEcharts(statusChartRef);

function renderChart(data: TestProjectStats) {
  const theme = getChartTheme();
  const titleTextStyle = { color: theme.text, fontSize: 14, fontWeight: 600 };
  const legendTextStyle = { color: theme.muted };
  renderStatusChart({
    aria: {
      enabled: true,
      decal: { show: true },
      label: { description: '测评项目进度状态分布饼图' },
    },
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
        data: buildStatusDistribution(data)
          .map((item, index) => ({
            name: item.label,
            value: item.value,
            itemStyle: { color: ['#1890ff', '#7b68ee', '#13c2c2'][index] },
          }))
          .filter((item) => item.value > 0),
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
        v-if="hasData"
        ref="statusChartRef"
        aria-label="测评项目进度状态分布图"
        class="chart-plot"
        height="100%"
        role="img"
      />
      <ElEmpty
        v-else-if="!loading"
        :image-size="64"
        description="暂无项目数据"
      />
    </div>
    <details class="chart-data-details">
      <summary>查看进度状态分布数据表</summary>
      <div class="chart-data-table-wrap">
        <table class="chart-data-table">
          <caption>
            测评项目进度状态分布
          </caption>
          <thead>
            <tr>
              <th scope="col">状态</th>
              <th scope="col">数量</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in statusDistribution" :key="item.label">
              <th scope="row">{{ item.label }}</th>
              <td>{{ item.value }}</td>
            </tr>
            <tr v-if="statusDistribution.length === 0">
              <td colspan="2">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </details>
  </div>
</template>

<style scoped src="./HomeCharts.css"></style>
