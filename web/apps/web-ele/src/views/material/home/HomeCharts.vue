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
  <div class="home-charts">
    <div class="home-chart-row">
      <HomeTypeChartCard :loading="loading" :stats="stats" />
      <HomeStatusChartCard :loading="loading" :stats="stats" />
    </div>

    <HomeMonthlyChartCard
      :loading="loading"
      :stats="stats"
      class="home-chart-monthly"
    />
  </div>
</template>

<style scoped src="./HomeCharts.css"></style>
