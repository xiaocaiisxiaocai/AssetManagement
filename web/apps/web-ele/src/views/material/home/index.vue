<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';

import { ElAlert, ElButton } from 'element-plus';

import {
  getTestProjectStatsApi,
  type TestProjectStats,
} from '#/api/test-project';
import { runHandled } from '#/utils/handled-promise';

import HomeCharts from './HomeCharts.vue';
import HomeSummaryCards from './HomeSummaryCards.vue';

const stats = ref<null | TestProjectStats>(null);
const statsLoading = ref(false);
const statsError = ref('');
let disposed = false;

async function loadStats() {
  if (statsLoading.value) return;
  statsLoading.value = true;
  try {
    const data = await getTestProjectStatsApi();
    if (disposed) return;
    stats.value = data;
    statsError.value = '';
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

      <HomeSummaryCards :stats="stats" />
      <HomeCharts :loading="statsLoading" :stats="stats" />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
