<script lang="ts" setup>
import type { AssetOverview } from '#/api/asset-overview';

import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';

import { ElEmpty } from 'element-plus';

import {
  buildCategoryDistribution,
  buildCategoryStatusSeries,
} from './chart-options';

const props = defineProps<{ stats: AssetOverview }>();
const distributionRef = ref<EchartsUIType>();
const statusRef = ref<EchartsUIType>();
const { renderEcharts: renderDistribution } = useEcharts(distributionRef);
const { renderEcharts: renderStatus } = useEcharts(statusRef);
let themeObserver: MutationObserver | undefined;
let disposed = false;

async function renderCharts() {
  await nextTick();
  if (disposed) return;
  const style = getComputedStyle(document.documentElement);
  const text = `hsl(${style.getPropertyValue('--foreground')})`;
  const muted = `hsl(${style.getPropertyValue('--muted-foreground')})`;
  const border = `hsl(${style.getPropertyValue('--border')})`;
  const data = props.stats;
  if (data.total > 0) {
    renderDistribution({
      aria: { enabled: true },
      backgroundColor: 'transparent',
      textStyle: { color: text },
      tooltip: {
        trigger: 'item',
        renderMode: 'richText',
        formatter: '{b}: {c} 条 ({d}%)',
      },
      legend: { type: 'scroll', bottom: 0, textStyle: { color: muted } },
      series: [
        {
          type: 'pie',
          radius: ['40%', '66%'],
          center: ['50%', '44%'],
          label: {
            show: true,
            color: text,
            fontSize: 12,
            formatter: '{b}\n{c} 条 ({d}%)',
          },
          labelLine: { show: true, length: 12, length2: 8 },
          data: buildCategoryDistribution(data).filter(
            (item) => item.value > 0,
          ),
        },
      ],
    });
  }
  if (data.categories.length > 0) {
    const series = buildCategoryStatusSeries(data);
    renderStatus({
      aria: { enabled: true },
      backgroundColor: 'transparent',
      textStyle: { color: text },
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        renderMode: 'richText',
      },
      legend: { top: 0, textStyle: { color: muted } },
      grid: { top: 40, left: 12, right: 24, bottom: 16, containLabel: true },
      xAxis: {
        type: 'value',
        minInterval: 1,
        axisLabel: { color: muted },
        splitLine: { lineStyle: { color: border } },
      },
      yAxis: {
        type: 'category',
        inverse: true,
        data: series.labels,
        axisLabel: { color: muted, width: 110, overflow: 'truncate' },
        axisTick: { show: false },
        axisLine: { lineStyle: { color: border } },
      },
      series: [
        {
          name: '在库',
          type: 'bar',
          stack: 'total',
          barMaxWidth: 24,
          label: {
            show: true,
            position: 'inside',
            formatter: (params) =>
              Number(params.value) > 0 ? `${params.value}` : '',
          },
          data: series.available,
          color: '#22a884',
        },
        {
          name: '借出',
          type: 'bar',
          stack: 'total',
          barMaxWidth: 24,
          label: {
            show: true,
            position: 'inside',
            formatter: (params) =>
              Number(params.value) > 0 ? `${params.value}` : '',
          },
          data: series.borrowed,
          color: '#e9a23b',
        },
        {
          name: '维修',
          type: 'bar',
          stack: 'total',
          barMaxWidth: 24,
          label: {
            show: true,
            position: 'inside',
            formatter: (params) =>
              Number(params.value) > 0 ? `${params.value}` : '',
          },
          data: series.maintenance,
          color: '#3b82d6',
        },
        {
          name: '报废',
          type: 'bar',
          stack: 'total',
          barMaxWidth: 24,
          label: {
            show: true,
            position: 'inside',
            formatter: (params) =>
              Number(params.value) > 0 ? `${params.value}` : '',
          },
          data: series.scrapped,
          color: '#d4564e',
        },
      ],
    });
  }
}

watch(() => props.stats, renderCharts);
onMounted(() => {
  void renderCharts();
  themeObserver = new MutationObserver(() => void renderCharts());
  themeObserver.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['class', 'data-theme', 'style'],
  });
});
onBeforeUnmount(() => {
  disposed = true;
  themeObserver?.disconnect();
});
</script>

<template>
  <section aria-label="二级分类资产图表" class="overview-charts">
    <div class="overview-chart-card">
      <h3>二级分类资产分布</h3>
      <EchartsUI
        v-show="stats.total > 0"
        ref="distributionRef"
        aria-label="二级分类资产数量占比，详细数据见下方分类汇总表"
        class="overview-pie"
        role="img"
      />
      <ElEmpty
        v-if="stats.total === 0"
        :image-size="72"
        description="暂无资产数据"
      />
    </div>
    <div class="overview-chart-card">
      <h3>二级分类状态分布</h3>
      <div v-show="stats.categories.length > 0" class="overview-bar-scroll">
        <EchartsUI
          ref="statusRef"
          :style="{
            height: `${Math.max(280, stats.categories.length * 38 + 70)}px`,
          }"
          aria-label="各二级分类在库、借出、维修和报废数量"
          role="img"
        />
      </div>
      <ElEmpty
        v-if="stats.categories.length === 0"
        :image-size="72"
        description="暂无二级分类"
      />
    </div>
  </section>
</template>

<style scoped src="./overview.css"></style>
