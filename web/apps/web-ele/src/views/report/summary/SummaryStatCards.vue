<script lang="ts" setup>
import type { AssetSummary } from '#/api/report';

defineProps<{
  summary: AssetSummary;
}>();
</script>

<template>
  <div class="stat-cards report-stat-cards">
    <div class="stat-card">
      <div class="stat-label">资产总数</div>
      <div class="stat-value">{{ summary.total }}</div>
    </div>
    <div class="stat-card">
      <div class="stat-label">在库资产</div>
      <div class="stat-value stat-value-success">
        {{ summary.available }}
      </div>
    </div>
    <div class="stat-card">
      <div class="stat-label">借出资产</div>
      <div class="stat-value stat-value-warning">
        {{ summary.borrowed }}
      </div>
    </div>
  </div>
</template>
