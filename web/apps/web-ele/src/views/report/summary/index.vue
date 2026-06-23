<script lang="ts" setup>
import type { AssetSummary } from '#/api/report';

import { onMounted, ref } from 'vue';

import { exportAssetSummaryApi, getAssetSummaryApi } from '#/api/report';

import { ElButton, ElTable, ElTableColumn, ElTag } from 'element-plus';

defineOptions({ name: 'ReportSummary' });

const loading = ref(false);
const summary = ref<AssetSummary>({
  available: 0,
  borrowed: 0,
  byCategory: [],
  byDept: [],
  total: 0,
});

async function loadData() {
  loading.value = true;
  try {
    summary.value = await getAssetSummaryApi();
  } finally {
    loading.value = false;
  }
}

async function exportReport() {
  const response = await exportAssetSummaryApi();
  downloadBlob(response.data, 'asset-summary.xlsx');
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">资产汇总</h2>
        </div>
        <div class="flex gap-2">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton type="primary" @click="exportReport">导出</ElButton>
        </div>
      </div>

      <div class="grid gap-3 md:grid-cols-3">
        <div class="rounded border bg-card p-4">
          <div class="text-sm text-muted-foreground">资产总数</div>
          <div class="mt-2 text-2xl font-semibold">{{ summary.total }}</div>
        </div>
        <div class="rounded border bg-card p-4">
          <div class="text-sm text-muted-foreground">在库资产</div>
          <div class="mt-2 text-2xl font-semibold">{{ summary.available }}</div>
        </div>
        <div class="rounded border bg-card p-4">
          <div class="text-sm text-muted-foreground">借出资产</div>
          <div class="mt-2 text-2xl font-semibold">{{ summary.borrowed }}</div>
        </div>
      </div>

      <div class="grid gap-4 xl:grid-cols-2">
        <div class="rounded border bg-card p-4">
          <div class="mb-3 font-medium">按分类统计</div>
          <ElTable v-loading="loading" :data="summary.byCategory" border>
            <ElTableColumn label="分类" min-width="180">
              <template #default="{ row }">
                <ElTag size="small">{{ row.categoryCode }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="总数" prop="total" width="80" />
            <ElTableColumn label="在库" prop="available" width="80" />
            <ElTableColumn label="借出" prop="borrowed" width="80" />
            <ElTableColumn label="占比" width="90">
              <template #default="{ row }">{{ row.percent }}%</template>
            </ElTableColumn>
          </ElTable>
        </div>

        <div class="rounded border bg-card p-4">
          <div class="mb-3 font-medium">按部门统计</div>
          <ElTable v-loading="loading" :data="summary.byDept" border>
            <ElTableColumn label="部门" min-width="180">
              <template #default="{ row }">
                <div>{{ row.departmentName }}</div>
              </template>
            </ElTableColumn>
            <ElTableColumn label="总数" prop="total" width="80" />
            <ElTableColumn label="在库" prop="available" width="80" />
            <ElTableColumn label="借出" prop="borrowed" width="80" />
            <ElTableColumn label="占比" width="90">
              <template #default="{ row }">{{ row.percent }}%</template>
            </ElTableColumn>
          </ElTable>
        </div>
      </div>
    </div>
  </re-page>
</template>
