<script lang="ts" setup>
import { onActivated, onBeforeUnmount, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { ElAlert, ElButton, ElEmpty, ElSkeleton } from 'element-plus';

import { type AssetOverview, getAssetOverviewApi } from '#/api/asset-overview';
import { runHandled } from '#/utils/handled-promise';

import OverviewCharts from './OverviewCharts.vue';

defineOptions({ name: 'AssetOverview' });

const router = useRouter();
const stats = ref<AssetOverview | null>(null);
const loading = ref(false);
const failed = ref(false);
let disposed = false;

async function loadOverview() {
  if (loading.value || disposed) return;
  loading.value = true;
  try {
    const data = await getAssetOverviewApi();
    if (disposed) return;
    stats.value = data;
    failed.value = false;
  } catch {
    if (!disposed) failed.value = true;
  } finally {
    if (!disposed) loading.value = false;
  }
}

onMounted(loadOverview);
onActivated(loadOverview);
onBeforeUnmount(() => {
  disposed = true;
});
</script>

<template>
  <re-page>
    <div class="asset-overview-page">
      <header class="overview-heading">
        <div>
          <h2>资产总览</h2>
        </div>
        <div class="overview-actions">
          <ElButton :loading="loading" @click="loadOverview">刷新</ElButton>
          <ElButton
            type="primary"
            @click="runHandled(router.push('/asset/list'))"
          >
            查看资产列表
          </ElButton>
        </div>
      </header>

      <ElAlert
        v-if="failed"
        :closable="false"
        show-icon
        title="资产统计暂时无法更新"
        type="warning"
      >
        {{
          stats
            ? '当前保留最近一次成功数据，请点击刷新重试。'
            : '请点击刷新重试，未加载的数据不会显示为 0。'
        }}
      </ElAlert>
      <ElSkeleton :loading="loading && !stats" :rows="8" animated>
        <template #default>
          <template v-if="stats">
            <section aria-label="资产统计指标" class="overview-metrics">
              <div class="overview-metric">
                <div class="overview-label">资产总数</div>
                <strong>{{ stats.total }}</strong>
              </div>
              <div class="overview-metric is-available">
                <div class="overview-label">在库资产</div>
                <strong>{{ stats.available }}</strong>
              </div>
              <div class="overview-metric is-borrowed">
                <div class="overview-label">借出资产</div>
                <strong>{{ stats.borrowed }}</strong>
              </div>
              <div class="overview-metric is-maintenance">
                <div class="overview-label">维修资产</div>
                <strong>{{ stats.maintenance }}</strong>
              </div>
              <div class="overview-metric is-scrapped">
                <div class="overview-label">报废资产</div>
                <strong>{{ stats.scrapped }}</strong>
              </div>
              <div class="overview-metric">
                <div class="overview-label">二级分类数</div>
                <strong>{{ stats.categories.length }}</strong>
              </div>
            </section>
            <ElAlert
              v-if="stats.uncategorizedTotal > 0"
              :closable="false"
              :title="`另有 ${stats.uncategorizedTotal} 条资产未归入有效二级分类，已计入资产总数及分布图。`"
              show-icon
              type="info"
            />
            <OverviewCharts :stats="stats" />
          </template>
          <ElEmpty v-else description="资产统计暂不可用，请点击刷新重试" />
        </template>
      </ElSkeleton>
    </div>
  </re-page>
</template>

<style scoped src="./overview.css"></style>
