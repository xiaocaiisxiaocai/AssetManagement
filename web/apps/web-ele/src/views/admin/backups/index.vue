<script lang="ts" setup>
import type { DatabaseBackupFile } from '#/api/report';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  backupDatabaseApi,
  downloadDatabaseBackupApi,
  getDatabaseBackupsApi,
} from '#/api/report';

import {
  ElButton,
  ElMessage,
  ElMessageBox,
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

const totalSize = computed(() => rows.value.reduce((sum, item) => sum + item.sizeBytes, 0));
const backupPath = computed(() => {
  const path = rows.value[0]?.filePath;
  if (!path) return '暂无备份文件';
  const normalized = path.replaceAll('\\', '/');
  return normalized.slice(0, normalized.lastIndexOf('/')) || path;
});

async function loadData() {
  loading.value = true;
  try {
    rows.value = await getDatabaseBackupsApi();
  } finally {
    loading.value = false;
  }
}

async function backupDatabase() {
  await ElMessageBox.confirm(
    '确认立即生成完整备份包？备份包会包含数据库 SQL 和附件目录，过程可能需要等待一段时间。',
    '生成完整备份包',
    {
      confirmButtonText: '开始备份',
      cancelButtonText: '取消',
      type: 'warning',
    },
  );

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

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="backup-page">
      <div class="backup-header">
        <div>
          <h2 class="backup-title">数据库备份</h2>
          <p class="backup-subtitle">查看、下载备份文件，并手动生成完整备份包</p>
        </div>
        <div class="backup-actions">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton v-if="canManageBackup" :loading="backupLoading" type="primary" @click="backupDatabase">
            生成完整备份包
          </ElButton>
        </div>
      </div>

      <div class="backup-notice">
        <div>
          <strong>完整备份包</strong>
          <span>包含当前业务数据库 SQL 与附件上传目录，可用于离线归档或人工恢复。</span>
        </div>
        <div>
          <strong>恢复提醒</strong>
          <span>恢复会覆盖现有数据，当前不提供页面一键恢复，建议由管理员在停机窗口手工执行。</span>
        </div>
      </div>

      <div class="backup-overview">
        <div class="overview-item">
          <span>备份文件</span>
          <strong>{{ rows.length }}</strong>
        </div>
        <div class="overview-item">
          <span>占用空间</span>
          <strong>{{ formatSize(totalSize) }}</strong>
        </div>
        <div class="overview-path">
          <span>备份目录</span>
          <strong>{{ backupPath }}</strong>
        </div>
      </div>

      <div class="backup-table-panel">
        <ElTable v-loading="loading" :data="rows" border height="100%">
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
.backup-notice,
.backup-overview,
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
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
}

.backup-notice {
  display: grid;
  flex-shrink: 0;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  padding: 14px 20px;
  background: var(--asset-page-surface-soft);
}

.backup-notice div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.backup-notice strong {
  color: var(--asset-page-text);
  font-size: 14px;
  line-height: 20px;
}

.backup-notice span {
  color: var(--asset-page-muted);
  font-size: 13px;
  line-height: 20px;
}

.backup-title {
  margin: 0 0 4px 0;
  color: var(--asset-page-text);
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
}

.backup-subtitle {
  margin: 0;
  color: var(--asset-page-muted);
  font-size: 14px;
  line-height: 20px;
}

.backup-actions {
  display: flex;
  gap: 8px;
}

.backup-overview {
  display: grid;
  flex-shrink: 0;
  grid-template-columns: 160px 160px minmax(0, 1fr);
  gap: 12px;
  padding: 16px 20px;
}

.overview-item,
.overview-path {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.overview-item span,
.overview-path span {
  color: var(--asset-page-muted);
  font-size: 13px;
  line-height: 18px;
}

.overview-item strong,
.overview-path strong {
  min-width: 0;
  overflow: hidden;
  color: var(--asset-page-text);
  font-size: 18px;
  font-weight: 600;
  line-height: 26px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.overview-path strong {
  font-size: 14px;
  line-height: 22px;
}

.backup-table-panel {
  flex: 1;
  display: flex;
  min-height: 0;
  flex-direction: column;
  padding: 20px;
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

  .backup-overview {
    grid-template-columns: 1fr;
  }

  .backup-notice {
    grid-template-columns: 1fr;
  }
}
</style>
