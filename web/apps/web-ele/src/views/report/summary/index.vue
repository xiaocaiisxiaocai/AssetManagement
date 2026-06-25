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
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">资产汇总报表</h2>
          <p class="page-subtitle">资产总览与分类统计</p>
        </div>
        <div class="page-actions">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton type="primary" @click="exportReport">导出</ElButton>
        </div>
      </div>

      <div class="stat-cards">
        <div class="stat-card">
          <div class="stat-label">资产总数</div>
          <div class="stat-value">{{ summary.total }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">在库资产</div>
          <div class="stat-value stat-value-success">{{ summary.available }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">借出资产</div>
          <div class="stat-value stat-value-warning">{{ summary.borrowed }}</div>
        </div>
      </div>

      <div class="summary-tables">
        <div class="summary-table-panel">
          <div class="summary-table-title">按分类统计</div>
          <ElTable v-loading="loading" :data="summary.byCategory" border>
            <ElTableColumn label="分类" min-width="180">
              <template #default="{ row }">
                <ElTag size="small">{{ row.categoryCode }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="总数" prop="total" width="100" align="center" />
            <ElTableColumn label="在库" prop="available" width="100" align="center" />
            <ElTableColumn class-name="hide-on-mobile" label="借出" prop="borrowed" width="100" align="center" />
            <ElTableColumn class-name="hide-on-mobile" label="占比" width="100" align="center">
              <template #default="{ row }">{{ row.percent }}%</template>
            </ElTableColumn>
          </ElTable>
        </div>

        <div class="summary-table-panel">
          <div class="summary-table-title">按部门统计</div>
          <ElTable v-loading="loading" :data="summary.byDept" border>
            <ElTableColumn label="部门" min-width="180">
              <template #default="{ row }">
                <div>{{ row.departmentName }}</div>
              </template>
            </ElTableColumn>
            <ElTableColumn label="总数" prop="total" width="100" align="center" />
            <ElTableColumn label="在库" prop="available" width="100" align="center" />
            <ElTableColumn class-name="hide-on-mobile" label="借出" prop="borrowed" width="100" align="center" />
            <ElTableColumn class-name="hide-on-mobile" label="占比" width="100" align="center">
              <template #default="{ row }">{{ row.percent }}%</template>
            </ElTableColumn>
          </ElTable>
        </div>
      </div>
    </div>
  </re-page>
</template>

<style scoped>
.summary-tables {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px;
}

.summary-table-panel {
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  padding: 20px;
}

.summary-table-title {
  margin-bottom: 16px;
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
  color: var(--asset-page-text);
}

.summary-table-panel :deep(.el-table) {
  font-size: 14px;
  line-height: 20px;
}

.summary-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.summary-table-panel :deep(.el-table--border) {
  border: none;
}

.summary-table-panel :deep(.el-table td.el-table__cell),
.summary-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.summary-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

@media (max-width: 1280px) {
  .summary-tables {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .summary-tables {
    gap: 12px;
  }

  .summary-table-panel {
    padding: 16px;
  }
}
</style>
