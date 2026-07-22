<script lang="ts" setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';

import { ElAlert, ElButton } from 'element-plus';

import {
  getTestProjectStatsApi,
  type TestProjectStats,
} from '#/api/test-project';
import { runHandled } from '#/utils/handled-promise';

import {
  buildMonthlySeriesData,
  buildMonthlyTableRows,
  buildStatusDistribution,
  monthLabels,
  quantityAxisLabel,
} from './chart-options';

const stats = ref<null | TestProjectStats>(null);
const statsLoading = ref(false);
const statsError = ref('');
const statusDistribution = computed(() =>
  stats.value ? buildStatusDistribution(stats.value) : [],
);
const monthlyTableRows = computed(() =>
  stats.value ? buildMonthlyTableRows(stats.value.monthlyStat) : [],
);

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
let disposed = false;

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

  // 状态分布饼图
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

  // 柱线组合图
  const { closedData, followUpData } = buildMonthlySeriesData(data.monthlyStat);
  renderBarChart({
    aria: {
      enabled: true,
      decal: { show: true },
      label: { description: '全年结案数量与跟进记录数量统计图' },
    },
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

function ensureThemeObserver() {
  if (themeObserver) return;
  themeObserver = new MutationObserver(() => {
    if (stats.value) renderCharts(stats.value);
  });
  themeObserver.observe(document.documentElement, {
    attributeFilter: ['class', 'data-theme', 'style'],
    attributes: true,
  });
}

async function loadStats() {
  if (statsLoading.value) return;
  statsLoading.value = true;
  try {
    const data = await getTestProjectStatsApi();
    if (disposed) return;
    stats.value = data;
    statsError.value = '';
    await nextTick();
    if (disposed) return;
    renderCharts(data);
    ensureThemeObserver();
  } catch (error) {
    if (disposed) return;
    statsError.value =
      error instanceof Error && error.message.trim()
        ? error.message
        : '统计数据加载失败';
  } finally {
    if (!disposed) statsLoading.value = false;
  }
}

onMounted(() => runHandled(loadStats()));

onBeforeUnmount(() => {
  disposed = true;
  themeObserver?.disconnect();
});
</script>

<template>
  <re-page>
    <div class="material-home-page p-4">
      <ElAlert
        v-if="statsError"
        :closable="false"
        show-icon
        title="统计数据暂时无法更新"
        type="warning"
      >
        <div class="stats-error-content">
          <span>已保留最近一次成功数据；未成功加载的指标显示为“—”。</span>
          <ElButton
            :loading="statsLoading"
            link
            type="warning"
            @click="loadStats"
          >
            重新加载
          </ElButton>
        </div>
      </ElAlert>

      <!-- 顶部统计卡片 -->
      <div class="summary-grid">
        <div class="summary-card summary-card-blue">
          <div class="stat-num text-blue-500">{{ stats?.total ?? '—' }}</div>
          <div class="stat-label">总测评数</div>
        </div>
        <div class="summary-card summary-card-green">
          <div class="stat-num text-green-500">{{ stats?.closed ?? '—' }}</div>
          <div class="stat-label">已结案</div>
        </div>
        <div class="summary-card summary-card-purple">
          <div class="stat-num text-purple-500">
            {{ stats?.inProgress ?? '—' }}
          </div>
          <div class="stat-label">计划/测试中</div>
        </div>
        <div class="summary-card summary-card-red">
          <div class="stat-num text-red-500">{{ stats?.landed ?? '—' }}</div>
          <div class="stat-label">落地跟进</div>
        </div>
      </div>

      <!-- 中间两个饼图 -->
      <div class="grid grid-cols-2 gap-4">
        <div class="chart-card" v-loading="statsLoading">
          <EchartsUI
            ref="typeChartRef"
            aria-label="测评类型分布图"
            role="img"
            style="height: 260px"
          />
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
        <div class="chart-card" v-loading="statsLoading">
          <EchartsUI
            ref="statusChartRef"
            aria-label="测评项目进度状态分布图"
            role="img"
            style="height: 260px"
          />
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
      </div>

      <!-- 底部柱线组合图 -->
      <div class="chart-card" v-loading="statsLoading">
        <EchartsUI
          ref="barChartRef"
          aria-label="全年结案与跟进记录统计图"
          role="img"
          style="height: 280px"
        />
        <details class="chart-data-details">
          <summary>查看全年结案与跟进数据表</summary>
          <div class="chart-data-table-wrap">
            <table class="chart-data-table">
              <caption>
                全年结案与跟进记录统计
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
    </div>
  </re-page>
</template>

<style scoped>
.material-home-page {
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: calc(var(--vben-content-height, 100vh) - 32px);
  min-height: 0;
  overflow: hidden;
}

.summary-grid {
  display: grid;
  flex-shrink: 0;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.stats-error-content {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.summary-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 118px;
  padding: 24px 32px;
  text-align: center;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  box-shadow: var(--asset-page-shadow);
}

.stat-num {
  @apply mb-2 text-5xl font-bold leading-none;
}

.stat-label {
  margin-top: 4px;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.chart-card {
  min-height: 0;
  padding: 12px;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  box-shadow: var(--asset-page-shadow);
}

.chart-data-details {
  margin-top: 8px;
  font-size: 14px;
  color: var(--asset-page-text-secondary);
}

.chart-data-details summary {
  display: flex;
  align-items: center;
  min-height: 44px;
  color: var(--el-color-primary);
  cursor: pointer;
}

.chart-data-table-wrap {
  overflow-x: auto;
}

.chart-data-table {
  width: 100%;
  color: var(--asset-page-text);
  border-collapse: collapse;
}

.chart-data-table caption {
  padding: 8px;
  font-weight: 600;
  text-align: left;
}

.chart-data-table th,
.chart-data-table td {
  padding: 8px 12px;
  text-align: left;
  border: 1px solid var(--asset-page-border);
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
