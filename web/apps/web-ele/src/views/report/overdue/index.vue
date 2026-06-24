<script lang="ts" setup>
import type { OverdueReportRow } from '#/api/report';

import { computed, onMounted, ref } from 'vue';

import {
  exportOverdueReportApi,
  getOverdueReportApi,
  remindOverdueApi,
  remindOverdueBatchApi,
} from '#/api/report';

import { ElButton, ElMessage, ElTable, ElTableColumn, ElTag } from 'element-plus';

defineOptions({ name: 'ReportOverdue' });

const loading = ref(false);
const reminding = ref(false);
const rows = ref<OverdueReportRow[]>([]);
const selectedRows = ref<OverdueReportRow[]>([]);
const seriousCount = computed(() => rows.value.filter((row) => row.isSerious).length);

async function loadData() {
  loading.value = true;
  try {
    rows.value = await getOverdueReportApi();
  } finally {
    loading.value = false;
  }
}

async function remind(row: OverdueReportRow) {
  reminding.value = true;
  try {
    await remindOverdueApi(row.assetId);
    ElMessage.success('站内催办已记录');
  } finally {
    reminding.value = false;
  }
}

async function remindBatch() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择逾期资产');
    return;
  }
  reminding.value = true;
  try {
    await remindOverdueBatchApi(selectedRows.value.map((row) => row.assetId));
    ElMessage.success('批量催办已记录');
  } finally {
    reminding.value = false;
  }
}

async function exportReport() {
  const response = await exportOverdueReportApi();
  downloadBlob(response.data, 'overdue-report.xlsx');
}

function onSelectionChange(selection: OverdueReportRow[]) {
  selectedRows.value = selection;
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
          <h2 class="page-title">逾期资产报表</h2>
          <p class="page-subtitle">逾期资产查询与催办</p>
        </div>
        <div class="page-actions">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton :loading="reminding" type="warning" @click="remindBatch">批量催办</ElButton>
          <ElButton type="primary" @click="exportReport">导出</ElButton>
        </div>
      </div>

      <div class="stat-cards">
        <div class="stat-card">
          <div class="stat-label">逾期资产</div>
          <div class="stat-value stat-value-warning">{{ rows.length }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">严重逾期</div>
          <div class="stat-value stat-value-danger">{{ seriousCount }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">已选资产</div>
          <div class="stat-value">{{ selectedRows.length }}</div>
        </div>
      </div>

      <div class="table-panel">
        <ElTable
          v-loading="loading"
          :data="rows"
          border
          row-key="assetId"
          @selection-change="onSelectionChange"
        >
          <ElTableColumn type="selection" width="48" />
          <ElTableColumn label="资产" min-width="220">
            <template #default="{ row }">
              <div>{{ row.assetName }}</div>
              <ElTag size="small">{{ row.assetNo }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="分类" min-width="170">
            <template #default="{ row }">
              <ElTag v-if="row.categoryCode" size="small">{{ row.categoryCode }}</ElTag>
              <span v-else class="empty-text">-</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="借用人" min-width="120" prop="borrower" />
          <ElTableColumn label="部门" min-width="120" prop="borrowerDept" />
          <ElTableColumn label="预计归还" min-width="120" prop="returnDate" />
          <ElTableColumn label="逾期天数" width="120" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isSerious ? 'danger' : 'warning'" size="small">
                {{ row.overdueDays }} 天
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="110" align="center">
            <template #default="{ row }">
              <ElButton :loading="reminding" link type="warning" size="small" @click="remind(row)">
                催办
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>
    </div>
  </re-page>
</template>
