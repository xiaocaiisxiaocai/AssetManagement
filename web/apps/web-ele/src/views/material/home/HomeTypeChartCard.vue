<script lang="ts" setup>
import type { TestProjectStats } from '#/api/test-project';

import { computed, ref } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';
import { ElEmpty } from 'element-plus';

import { getChartTheme } from './home-chart-theme';
import { useHomeChartCard } from './use-home-chart-card';

const props = defineProps<{
  loading: boolean;
  stats: null | TestProjectStats;
}>();
const hasData = computed(() =>
  props.stats?.typeDist.some((item) => item.count > 0),
);

const typeChartRef = ref<EchartsUIType>();
const { renderEcharts: renderTypeChart } = useEcharts(typeChartRef);

function renderChart(data: TestProjectStats) {
  const theme = getChartTheme();
  const titleTextStyle = { color: theme.text, fontSize: 14, fontWeight: 600 };
  const legendTextStyle = { color: theme.muted };
  renderTypeChart({
    aria: {
      enabled: true,
      decal: { show: true },
      label: { description: '测评类型分布饼图' },
    },
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
}

useHomeChartCard(() => props.stats, renderChart);
</script>

<template>
  <div class="chart-card" v-loading="loading">
    <EchartsUI
      v-if="hasData"
      ref="typeChartRef"
      aria-label="测评类型分布图"
      role="img"
      style="height: 260px"
    />
    <ElEmpty v-else-if="!loading" :image-size="64" description="暂无项目数据" />
    <details class="chart-data-details">
      <summary>查看测评类型分布数据表</summary>
      <div class="chart-data-table-wrap">
        <table class="chart-data-table">
          <caption>
            测评类型分布
          </caption>
          <thead>
            <tr>
              <th scope="col">类型</th>
              <th scope="col">数量</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in stats?.typeDist ?? []" :key="item.label">
              <th scope="row">{{ item.label }}</th>
              <td>{{ item.count }}</td>
            </tr>
            <tr v-if="!stats?.typeDist.length">
              <td colspan="2">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </details>
  </div>
</template>

<style scoped src="./HomeCharts.css"></style>
