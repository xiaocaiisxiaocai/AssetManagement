<script lang="ts" setup>
defineProps<{
  selectedCount: number;
  seriousTotal: number;
  total: number;
}>();
</script>

<template>
  <div class="stat-cards report-stat-cards">
    <div class="stat-card">
      <div class="stat-label">逾期资产</div>
      <div class="stat-value stat-value-warning">{{ total }}</div>
    </div>
    <div class="stat-card">
      <div class="stat-label">严重逾期</div>
      <div class="stat-value stat-value-danger">{{ seriousTotal }}</div>
    </div>
    <div class="stat-card">
      <div class="stat-label">已选资产</div>
      <div class="stat-value">{{ selectedCount }}</div>
    </div>
  </div>
</template>
