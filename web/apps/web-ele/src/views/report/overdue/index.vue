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
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">逾期资产</h2>
        </div>
        <div class="flex flex-wrap gap-2">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton :loading="reminding" type="warning" @click="remindBatch">批量催办</ElButton>
          <ElButton type="primary" @click="exportReport">导出</ElButton>
        </div>
      </div>

      <div class="grid gap-3 md:grid-cols-3">
        <div class="rounded border bg-card p-4">
          <div class="text-sm text-muted-foreground">逾期资产</div>
          <div class="mt-2 text-2xl font-semibold">{{ rows.length }}</div>
        </div>
        <div class="rounded border bg-card p-4">
          <div class="text-sm text-muted-foreground">严重逾期</div>
          <div class="mt-2 text-2xl font-semibold">{{ seriousCount }}</div>
        </div>
        <div class="rounded border bg-card p-4">
          <div class="text-sm text-muted-foreground">已选资产</div>
          <div class="mt-2 text-2xl font-semibold">{{ selectedRows.length }}</div>
        </div>
      </div>

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
            <span v-else>-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="借用人" min-width="120" prop="borrower" />
        <ElTableColumn label="部门" min-width="120" prop="borrowerDept" />
        <ElTableColumn label="预计归还" min-width="120" prop="returnDate" />
        <ElTableColumn label="逾期天数" width="110">
          <template #default="{ row }">
            <ElTag :type="row.isSerious ? 'danger' : 'warning'">
              {{ row.overdueDays }} 天
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="110">
          <template #default="{ row }">
            <ElButton :loading="reminding" link type="warning" @click="remind(row)">
              催办
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </div>
  </re-page>
</template>
