<script lang="ts" setup>
import type { TestProjectStats } from '#/api/test-project';

import HomeMonthlyChartCard from './HomeMonthlyChartCard.vue';
import HomeStatusChartCard from './HomeStatusChartCard.vue';
import HomeTypeChartCard from './HomeTypeChartCard.vue';

defineProps<{
  loading: boolean;
  stats: null | TestProjectStats;
}>();
</script>

<template>
  <div class="grid grid-cols-2 gap-4">
    <HomeTypeChartCard :loading="loading" :stats="stats" />
    <HomeStatusChartCard :loading="loading" :stats="stats" />
  </div>

  <HomeMonthlyChartCard :loading="loading" :stats="stats" />
</template>

<style scoped src="./HomeCharts.css"></style>
