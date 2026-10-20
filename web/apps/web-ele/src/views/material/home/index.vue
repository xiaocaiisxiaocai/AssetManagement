<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';

import { useRouter } from 'vue-router';

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
const router = useRouter();
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
      <header class="material-home-heading">
        <div>
          <h1>项目总览</h1>
          <p>
            {{
              stats
                ? stats.year + ' 年度项目、进度和落地跟进概况'
                : '项目、进度和落地跟进概况'
            }}
          </p>
        </div>
        <div class="material-home-heading-actions">
          <ElButton :loading="statsLoading" @click="loadStats">刷新</ElButton>
          <ElButton type="primary" @click="router.push('/material/projects')">
            查看测试项目
          </ElButton>
        </div>
      </header>
      <ElAlert
        v-if="statsError"
        :closable="false"
        show-icon
        title="统计数据暂时无法更新"
        type="warning"
      >
        <div class="stats-error-content">
          <span>{{
            stats
              ? '已保留最近一次成功数据；未成功加载的指标显示为“—”。'
              : '当前未能加载统计数据，请检查服务后重试。'
          }}</span>
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
