<script lang="ts" setup>
import type { OverdueReportRow } from '#/api/report';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import { ElButton, ElMessage } from 'element-plus';

import {
  exportOverdueReportApi,
  getOverdueReportApi,
  remindOverdueApi,
  remindOverdueBatchApi,
} from '#/api/report';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildReportActionAccess } from '#/views/permissions/action-access';

import OverdueStatCards from './OverdueStatCards.vue';
import OverdueTable from './OverdueTable.vue';

defineOptions({ name: 'ReportOverdue' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const reportActionAccess = computed(() =>
  buildReportActionAccess(hasAccessByCodes),
);
const listRequestGuard = createLatestRequestGuard();
const loading = ref(false);
const exporting = ref(false);
const remindingId = ref<null | number>(null);
const rows = ref<OverdueReportRow[]>([]);
const total = ref(0);
const seriousTotal = ref(0);
const selectedRows = ref<OverdueReportRow[]>([]);
const pageSizeOptions = ref(createPageSizeOptions(20));
const query = reactive({
  page: 1,
  pageSize: 20,
});
const queryModel = reactiveObjectModel(query);
async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  try {
    const result = await getOverdueReportApi(query);
    if (!listRequestGuard.isLatest(requestGeneration)) return;
    rows.value = result.items;
    total.value = result.total;
    seriousTotal.value = result.seriousTotal;
    selectedRows.value = [];
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

async function remind(row: OverdueReportRow) {
  remindingId.value = row.assetId;
  try {
    await remindOverdueApi(row.assetId);
    ElMessage.success('站内催办已记录');
  } finally {
    remindingId.value = null;
  }
}

async function exportReport() {
  if (exporting.value) return;
  exporting.value = true;
  try {
    const response = await exportOverdueReportApi();
    downloadBlob(response.data, '逾期资产.xlsx');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    exporting.value = false;
  }
}

async function remindBatch() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择逾期资产');
    return;
  }
  remindingId.value = -1;
  try {
    await remindOverdueBatchApi(selectedRows.value.map((row) => row.assetId));
    ElMessage.success('批量催办已记录');
  } finally {
    remindingId.value = null;
  }
}

function onSelectionChange(selection: OverdueReportRow[]) {
  selectedRows.value = selection;
}

async function onPageSizeChange() {
  query.page = 1;
  selectedRows.value = [];
  await loadData();
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

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container page-fit">
      <div class="page-header">
        <div>
          <h2 class="page-title">逾期资产报表</h2>
        </div>
        <div class="page-actions">
          <span class="column-tools-host" data-column-tools="report-overdue"></span>
          <ElButton
            v-if="reportActionAccess.canExport"
            :loading="exporting"
            @click="exportReport"
          >
            导出
          </ElButton>
          <ElButton
            v-if="reportActionAccess.canRemind"
            :loading="remindingId === -1"
            type="warning"
            @click="remindBatch"
          >
            批量催办
          </ElButton>
        </div>
      </div>

      <OverdueStatCards
        :selected-count="selectedRows.length"
        :serious-total="seriousTotal"
        :total="total"
      />
      <OverdueTable
        v-model:query="queryModel"
        :can-remind="reportActionAccess.canRemind"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :reminding-id="remindingId"
        :rows="rows"
        :total="total"
        @category-click="goCategoryAssets"
        @page-change="loadData"
        @page-size-change="onPageSizeChange"
        @remind="remind"
        @selection-change="onSelectionChange"
      />
    </div>
  </re-page>
</template>
