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
    <div class="summary-page">
      <div class="summary-header">
        <div>
          <h2 class="summary-title">资产汇总报表</h2>
          <p class="summary-subtitle">资产总览与分类统计</p>
        </div>
        <div class="summary-actions">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton type="primary" @click="exportReport">导出</ElButton>
        </div>
      </div>

      <div class="summary-cards">
        <div class="summary-stat-card">
          <div class="summary-stat-label">资产总数</div>
          <div class="summary-stat-value">{{ summary.total }}</div>
        </div>
        <div class="summary-stat-card">
          <div class="summary-stat-label">在库资产</div>
          <div class="summary-stat-value summary-stat-available">{{ summary.available }}</div>
        </div>
        <div class="summary-stat-card">
          <div class="summary-stat-label">借出资产</div>
          <div class="summary-stat-value summary-stat-borrowed">{{ summary.borrowed }}</div>
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
            <ElTableColumn label="借出" prop="borrowed" width="100" align="center" />
            <ElTableColumn label="占比" width="100" align="center">
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
            <ElTableColumn label="借出" prop="borrowed" width="100" align="center" />
            <ElTableColumn label="占比" width="100" align="center">
              <template #default="{ row }">{{ row.percent }}%</template>
            </ElTableColumn>
          </ElTable>
        </div>
      </div>
    </div>
  </re-page>
</template>

<style scoped>
/* ========== 设计系统规范 ========== */
.summary-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
  min-height: calc(100vh - 112px);
}

/* ========== 页面头部 ========== */
.summary-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border: 1px solid #e8e9eb;
  border-radius: 12px;
  background: linear-gradient(135deg, #ffffff 0%, #f8f9fa 100%);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.summary-title {
  margin: 0 0 4px 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: #1e293b;
  letter-spacing: -0.02em;
}

.summary-subtitle {
  margin: 0;
  font-size: 14px;
  line-height: 20px;
  color: #64748b;
}

.summary-actions {
  display: flex;
  gap: 12px;
}

/* ========== 统计卡片 ========== */
.summary-cards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 20px;
}

.summary-stat-card {
  padding: 24px;
  border: 1px solid #e8e9eb;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.summary-stat-label {
  margin-bottom: 12px;
  font-size: 14px;
  line-height: 20px;
  color: #64748b;
}

.summary-stat-value {
  font-size: 32px;
  font-weight: 600;
  line-height: 40px;
  color: #1e293b;
}

.summary-stat-available {
  color: #10b981;
}

.summary-stat-borrowed {
  color: #f59e0b;
}

/* ========== 表格区域 ========== */
.summary-tables {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 20px;
}

.summary-table-panel {
  border: 1px solid #e8e9eb;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  padding: 20px;
}

.summary-table-title {
  margin-bottom: 16px;
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
  color: #1e293b;
}

.summary-table-panel :deep(.el-table) {
  font-size: 14px;
  line-height: 20px;
}

.summary-table-panel :deep(.el-table th.el-table__cell) {
  background: #f8f9fa;
  color: #475569;
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.summary-table-panel :deep(.el-table--border) {
  border: none;
}

.summary-table-panel :deep(.el-table td.el-table__cell),
.summary-table-panel :deep(.el-table th.el-table__cell) {
  border-color: #e8e9eb;
}

.summary-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

/* ========== 响应式 ========== */
@media (max-width: 1280px) {
  .summary-tables {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .summary-cards {
    grid-template-columns: 1fr;
  }

  .summary-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }
}
</style>
