<script lang="ts" setup>
import type { AssetSummary } from '#/api/report';

import { ElTable, ElTableColumn } from 'element-plus';

defineProps<{
  loading: boolean;
  summary: AssetSummary;
}>();

const emit = defineEmits<{
  categoryClick: [categoryCode: string];
}>();
</script>

<template>
  <div class="summary-tables page-fill">
    <div class="summary-table-panel">
      <div class="summary-table-title">按分类统计</div>
      <ElTable :data="summary.byCategory" border v-loading="loading">
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
        <ElTableColumn align="center" label="总数" prop="total" width="100" />
        <ElTableColumn
          align="center"
          label="在库"
          prop="available"
          width="100"
        />
        <ElTableColumn
          align="center"
          class-name="hide-on-mobile"
          label="借出"
          prop="borrowed"
          width="100"
        />
        <ElTableColumn
          align="center"
          class-name="hide-on-mobile"
          label="占比"
          width="100"
        >
          <template #default="{ row }">{{ row.percent }}%</template>
        </ElTableColumn>
      </ElTable>
    </div>

    <div class="summary-table-panel">
      <div class="summary-table-title">按部门统计</div>
      <ElTable :data="summary.byDept" border v-loading="loading">
        <ElTableColumn label="部门" min-width="180">
          <template #default="{ row }">
            <div>{{ row.departmentName }}</div>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="总数" prop="total" width="100" />
        <ElTableColumn
          align="center"
          label="在库"
          prop="available"
          width="100"
        />
        <ElTableColumn
          align="center"
          class-name="hide-on-mobile"
          label="借出"
          prop="borrowed"
          width="100"
        />
        <ElTableColumn
          align="center"
          class-name="hide-on-mobile"
          label="占比"
          width="100"
        >
          <template #default="{ row }">{{ row.percent }}%</template>
        </ElTableColumn>
      </ElTable>
    </div>
  </div>
</template>

<style scoped src="./SummaryTables.css"></style>
