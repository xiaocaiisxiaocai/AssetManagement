<script lang="ts" setup>
import type { AssetSummary } from '#/api/report';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import { ElButton, ElTable, ElTableColumn } from 'element-plus';

import { exportAssetSummaryApi, getAssetSummaryApi } from '#/api/report';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';

defineOptions({ name: 'ReportSummary' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const canExport = computed(() => hasAccessByCodes(['report:export']));
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

function goCategoryAssets(categoryCode: string) {
  if (!categoryCode) return;
  runHandled(
    router.push({
      path: '/asset/list',
      query: { categoryCode },
    }),
  );
}

async function exportReport() {
  const response = await exportAssetSummaryApi();
  downloadBlob(response.data, '资产汇总.xlsx');
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <h2 class="page-title">资产汇总</h2>
        <ElButton v-if="canExport" type="primary" @click="exportReport">
          导出 Excel
        </ElButton>
      </div>
      <div class="stat-cards">
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

      <div class="summary-tables">
        <div class="summary-table-panel">
          <div class="summary-table-title">按分类统计</div>
          <ElTable
            :data="summary.byCategory"
            border
            height="100%"
            v-loading="loading"
          >
            <ElTableColumn label="分类" min-width="180">
              <template #default="{ row }">
                <button
                  class="category-code-link"
                  type="button"
                  @click="goCategoryAssets(row.categoryCode)"
                >
                  {{ row.categoryCode }}
                </button>
              </template>
            </ElTableColumn>
            <ElTableColumn
              align="right"
              label="总数"
              prop="total"
              width="100"
            />
            <ElTableColumn
              align="right"
              label="在库"
              prop="available"
              width="100"
            />
            <ElTableColumn
              align="right"
              class-name="hide-on-mobile"
              label="借出"
              prop="borrowed"
              width="100"
            />
            <ElTableColumn
              align="right"
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
          <ElTable
            :data="summary.byDept"
            border
            height="100%"
            v-loading="loading"
          >
            <ElTableColumn label="部门" min-width="180">
              <template #default="{ row }">
                <div>{{ row.departmentName }}</div>
              </template>
            </ElTableColumn>
            <ElTableColumn
              align="right"
              label="总数"
              prop="total"
              width="100"
            />
            <ElTableColumn
              align="right"
              label="在库"
              prop="available"
              width="100"
            />
            <ElTableColumn
              align="right"
              class-name="hide-on-mobile"
              label="借出"
              prop="borrowed"
              width="100"
            />
            <ElTableColumn
              align="right"
              class-name="hide-on-mobile"
              label="占比"
              width="100"
            >
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
  flex: 1;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px;
  min-height: 0;
}

.summary-table-panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
  padding: 20px;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

.summary-table-title {
  flex-shrink: 0;
  margin-bottom: 16px;
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
  color: var(--asset-page-text);
}

.summary-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
  font-size: 14px;
  line-height: 20px;
}

.summary-table-panel :deep(.el-table th.el-table__cell) {
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
  background: var(--asset-page-surface-soft);
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
