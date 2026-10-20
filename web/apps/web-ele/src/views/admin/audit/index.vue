<script lang="ts" setup>
import type { AuditListQuery } from './audit-query';

import type {
  AuditCleanupPreview,
  AuditLogQuery,
  AuditLogRow,
} from '#/api/report';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  cleanupAuditLogsApi,
  exportAuditLogsApi,
  getAuditCleanupPreviewApi,
  getAuditLogsApi,
} from '#/api/report';
import { formatDateTime } from '#/utils/date-format';
import { endOfSelectedDay, startOfSelectedDay } from '#/utils/date-range';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';

import AuditCleanupDialog from './AuditCleanupDialog.vue';
import AuditFilter from './AuditFilter.vue';
import AuditHeader from './AuditHeader.vue';
import AuditTable from './AuditTable.vue';

defineOptions({ name: 'AdminAudit' });

const listRequestGuard = createLatestRequestGuard();

const { hasAccessByCodes } = useAccess();
const canCleanupAudit = computed(() => hasAccessByCodes(['audit:cleanup']));
const canExportAudit = computed(() => hasAccessByCodes(['audit:export']));
const loading = ref(false);
const cleanupLoading = ref(false);
const cleanupPreviewLoading = ref(false);
const cleanupDialogVisible = ref(false);
const exporting = ref(false);
const rows = ref<AuditLogRow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));
const cleanupRetentionDays = ref(30);
const cleanupPreview = ref<AuditCleanupPreview | null>(null);
const query = reactive<AuditListQuery>({
  actionTypes: [],
  dateRange: [],
  module: '',
  page: 1,
  pageSize: 20,
  userId: undefined,
});
const queryModel = reactiveObjectModel(query);

async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  try {
    const result = await getAuditLogsApi(buildQuery());
    if (!listRequestGuard.isLatest(requestGeneration)) return;
    rows.value = result.items;
    total.value = result.total;
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

function buildQuery(): AuditLogQuery {
  return {
    actionTypes: query.actionTypes,
    endTime: endOfSelectedDay(query.dateRange[1]),
    module: query.module || undefined,
    page: query.page,
    pageSize: query.pageSize,
    startTime: startOfSelectedDay(query.dateRange[0]),
    userId: query.userId,
  };
}

function search() {
  query.page = 1;
  runHandled(loadData());
}

function resetQuery() {
  Object.assign(query, {
    actionTypes: [],
    dateRange: [],
    module: '',
    page: 1,
    userId: undefined,
  });
  runHandled(loadData());
}

async function exportAuditLogs() {
  if (exporting.value) return;
  exporting.value = true;
  try {
    const response = await exportAuditLogsApi(buildQuery());
    downloadBlob(response.data, '审计日志.xlsx');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    exporting.value = false;
  }
}

async function openCleanupDialog() {
  cleanupDialogVisible.value = true;
  await loadCleanupPreview();
}

async function loadCleanupPreview() {
  cleanupPreviewLoading.value = true;
  try {
    cleanupPreview.value = await getAuditCleanupPreviewApi(
      cleanupRetentionDays.value,
    );
  } finally {
    cleanupPreviewLoading.value = false;
  }
}

async function confirmCleanup() {
  const preview =
    cleanupPreview.value ??
    (await getAuditCleanupPreviewApi(cleanupRetentionDays.value));
  try {
    await ElMessageBox.confirm(
      `确认清理 ${formatDateTime(preview.cutoffTime, { seconds: true })} 之前的审计日志？预计删除 ${preview.deleteCount} 条。`,
      '清理审计日志',
      {
        confirmButtonText: '确认清理',
        cancelButtonText: '取消',
        type: 'warning',
      },
    );
  } catch {
    return;
  }

  cleanupLoading.value = true;
  try {
    const result = await cleanupAuditLogsApi(cleanupRetentionDays.value);
    ElMessage.success(`已清理 ${result.deletedCount} 条审计日志`);
    cleanupDialogVisible.value = false;
    await loadData();
  } finally {
    cleanupLoading.value = false;
  }
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
      <AuditHeader
        :can-cleanup="canCleanupAudit"
        :can-export="canExportAudit"
        :exporting="exporting"
        @cleanup="openCleanupDialog"
        @export="exportAuditLogs"
      />
      <AuditFilter
        v-model:query="queryModel"
        @reset="resetQuery"
        @search="search"
      />
      <AuditTable
        v-model:query="queryModel"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :rows="rows"
        :total="total"
        @page-change="loadData"
        @search="search"
      />
      <AuditCleanupDialog
        v-model:retention-days="cleanupRetentionDays"
        v-model:visible="cleanupDialogVisible"
        :cleanup-loading="cleanupLoading"
        :preview="cleanupPreview"
        :preview-loading="cleanupPreviewLoading"
        @confirm="confirmCleanup"
        @preview="loadCleanupPreview"
      />
    </div>
  </re-page>
</template>
