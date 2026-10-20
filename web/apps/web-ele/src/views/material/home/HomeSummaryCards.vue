<script lang="ts" setup>
import type { TestProjectStats } from '#/api/test-project';

defineProps<{
  stats: null | TestProjectStats;
}>();
</script>

<template>
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
      <div class="stat-label">规划/测试中项目</div>
    </div>
    <div class="summary-card summary-card-red">
      <div class="stat-num text-red-500">{{ stats?.landed ?? '—' }}</div>
      <div class="stat-label">落地跟进</div>
    </div>
  </div>
</template>

<style scoped src="./HomeSummaryCards.css"></style>
