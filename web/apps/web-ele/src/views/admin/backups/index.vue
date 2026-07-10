<script lang="ts" setup>
import type { DatabaseBackupFile } from '#/api/report';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  backupDatabaseApi,
  downloadDatabaseBackupApi,
  getDatabaseBackupsApi,
} from '#/api/report';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';

import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'AdminBackups' });

const { hasAccessByCodes } = useAccess();
const canManageBackup = computed(() => hasAccessByCodes(['backup:manage']));
const loading = ref(false);
const backupLoading = ref(false);
const rows = ref<DatabaseBackupFile[]>([]);
const page = ref(1);
const pageSize = ref(20);
const pageSizeOptions = ref(createPageSizeOptions(20));

const pagedRows = computed(() => {
  const start = (page.value - 1) * pageSize.value;
  return rows.value.slice(start, start + pageSize.value);
});

async function loadData() {
  loading.value = true;
  try {
    rows.value = await getDatabaseBackupsApi();
    if ((page.value - 1) * pageSize.value >= rows.value.length) {
      page.value = 1;
    }
  } finally {
    loading.value = false;
  }
}

async function backupDatabase() {
  try {
    await ElMessageBox.confirm(
      '确认立即生成完整备份包？备份包会包含数据库 SQL 和附件目录，过程可能需要等待一段时间。',
      '生成完整备份包',
      {
        confirmButtonText: '开始备份',
        cancelButtonText: '取消',
        type: 'warning',
      },
    );
  } catch {
    return;
  }

  backupLoading.value = true;
  try {
    const result = await backupDatabaseApi();
    ElMessage.success(`备份完成：${result.filePath}`);
    await loadData();
  } finally {
    backupLoading.value = false;
  }
}

async function downloadBackup(row: DatabaseBackupFile) {
  const response = await downloadDatabaseBackupApi(row.fileName);
  downloadBlob(response.data, row.fileName);
}

function formatTime(value: string) {
  return value?.replace('T', ' ').slice(0, 19);
}

function formatSize(bytes: number) {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index += 1;
  }
  return `${value.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}

function fileTypeLabel(type: string) {
  return type === 'package' ? '完整包' : 'SQL';
}

function fileTypeTag(type: string) {
  return type === 'package' ? 'success' : 'info';
}

function onPageSizeChange() {
  page.value = 1;
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

onMounted(async () => {
  pageSize.value = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(pageSize.value);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="backup-page">
      <div class="backup-header">
        <div>
          <h2 class="backup-title">数据库备份</h2>
        </div>
        <div class="backup-actions">
          <ElButton v-if="canManageBackup" :loading="backupLoading" type="primary" @click="backupDatabase">
            生成完整备份包
          </ElButton>
        </div>
      </div>

      <div class="backup-table-panel">
        <ElTable v-loading="loading" :data="pagedRows" border height="100%">
          <ElTableColumn label="文件名" min-width="260">
            <template #default="{ row }">
              <div class="file-name-cell">
                <ElTag size="small" :type="fileTypeTag(row.fileType)">
                  {{ fileTypeLabel(row.fileType) }}
                </ElTag>
                <span>{{ row.fileName }}</span>
              </div>
            </template>
          </ElTableColumn>
          <ElTableColumn label="类型" width="110" align="center">
            <template #default="{ row }">{{ fileTypeLabel(row.fileType) }}</template>
          </ElTableColumn>
          <ElTableColumn label="大小" width="120" align="right">
            <template #default="{ row }">{{ formatSize(row.sizeBytes) }}</template>
          </ElTableColumn>
          <ElTableColumn label="创建时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="完整路径" min-width="360" prop="filePath" />
          <ElTableColumn v-if="canManageBackup" fixed="right" label="操作" width="100" align="center">
            <template #default="{ row }">
              <ElButton link type="primary" size="small" @click="downloadBackup(row)">下载</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ rows.length }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect v-model="pageSize" style="width: 92px" @change="onPageSizeChange">
              <ElOption
                v-for="size in pageSizeOptions"
                :key="size"
                :label="`${size}`"
                :value="size"
              />
            </ElSelect>
          </div>
          <ElPagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="rows.length"
            background
            layout="prev, pager, next"
          />
        </div>
      </div>
    </div>
  </re-page>
</template>

<style scoped>
.backup-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}

.backup-header,
.backup-table-panel {
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.backup-header {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-color: var(--asset-page-border-strong);
  background: var(--asset-page-surface);
}

.backup-title {
  margin: 0;
  color: var(--asset-page-text);
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
}

.backup-actions {
  display: flex;
  gap: 8px;
}

.backup-table-panel {
  flex: 1;
  display: flex;
  min-height: 0;
  flex-direction: column;
  border-color: var(--asset-page-border);
  overflow: hidden;
}

.backup-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
}

.backup-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
  font-weight: 600;
}

.backup-table-panel :deep(.el-table--border) {
  border: none;
}

.backup-table-panel :deep(.el-table td.el-table__cell),
.backup-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.file-name-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.file-name-cell span:last-child {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 768px) {
  .backup-header {
    align-items: flex-start;
    flex-direction: column;
    gap: 12px;
  }

}
</style>
