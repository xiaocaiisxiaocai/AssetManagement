<script lang="ts" setup>
import type { AssetSummary, CategoryStatRow } from '#/api/report';

import {
  ElButton,
  ElCard,
  ElEmpty,
  ElTable,
  ElTableColumn,
} from 'element-plus';

defineProps<{
  categoryTopRows: CategoryStatRow[];
  seriousOverdueCount: null | number;
  summary: AssetSummary | null;
}>();

const emit = defineEmits<{
  categoryClick: [categoryCode: string];
  navigate: [path: null | string];
}>();

function formatCount(value: null | number) {
  return value ?? '—';
}
</script>

<template>
  <ElCard class="workspace-panel" shadow="never">
    <template #header>
      <div class="workspace-card-header">
        <span>资产概况</span>
        <ElButton
          link
          type="primary"
          @click="emit('navigate', '/report/summary')"
        >
          查看汇总
        </ElButton>
      </div>
    </template>
    <div class="workspace-summary">
      <div class="workspace-summary-item">
        <span>严重逾期</span>
        <strong>{{ formatCount(seriousOverdueCount) }}</strong>
      </div>
    </div>
    <ElTable
      v-if="summary"
      :data="categoryTopRows"
      border
      class="workspace-table workspace-summary-table"
    >
      <ElTableColumn label="分类" min-width="180">
        <template #default="{ row }">
          <button
            class="category-code-link"
            type="button"
            @click="emit('categoryClick', row.categoryCode)"
          >
            {{ row.categoryCode }}
          </button>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="总数" prop="total" width="90" />
      <ElTableColumn align="center" label="在库" prop="available" width="90" />
      <ElTableColumn align="center" label="借出" prop="borrowed" width="90" />
    </ElTable>
    <ElEmpty v-else description="资产概况暂不可用" />
  </ElCard>
</template>

<style scoped src="./workspace-panel.css"></style>
<style scoped src="./AssetOverviewPanel.css"></style>
