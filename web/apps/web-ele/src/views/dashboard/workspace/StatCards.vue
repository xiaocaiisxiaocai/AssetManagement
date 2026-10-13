<script lang="ts" setup>
import { ElSkeleton } from 'element-plus';

defineProps<{
  initialLoading: boolean;
  metricCards: {
    label: string;
    path: string;
    tone: string;
    value: number | string;
  }[];
}>();

const emit = defineEmits<{
  navigate: [path: null | string];
}>();
</script>

<template>
  <ElSkeleton :loading="initialLoading" animated>
    <template #template>
      <div class="stat-cards">
        <div v-for="index in 4" :key="index" class="stat-card">
          <ElSkeleton :rows="2" animated />
        </div>
      </div>
    </template>

    <template #default>
      <section class="stat-cards workspace-stat-grid">
        <button
          v-for="item in metricCards"
          :key="item.label"
          :class="`workspace-stat-${item.tone}`"
          class="stat-card workspace-stat-card"
          type="button"
          @click="emit('navigate', item.path)"
        >
          <div class="stat-label">{{ item.label }}</div>
          <div class="stat-value">{{ item.value }}</div>
        </button>
      </section>
    </template>
  </ElSkeleton>
</template>

<style scoped src="./StatCards.css"></style>
