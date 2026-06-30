<script lang="ts" setup>
import type { DatabaseBackupFile } from '#/api/report';

import { computed, onMounted, ref } from 'vue';

import { backupDatabaseApi, getDatabaseBackupsApi } from '#/api/report';

import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'AdminBackups' });

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
    '确认立即执行一次数据库备份？备份过程可能需要等待一段时间。',
    '立即备份',
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

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="backup-page">
      <div class="backup-header">
        <div>
          <h2 class="backup-title">数据库备份</h2>
          <p class="backup-subtitle">查看备份文件并手动触发数据库备份</p>
        </div>
        <div class="backup-actions">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton :loading="backupLoading" type="primary" @click="backupDatabase">
            立即备份
          </ElButton>
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
        <ElTable v-loading="loading" :data="rows" border>
          <ElTableColumn label="文件名" min-width="260">
            <template #default="{ row }">
              <div class="file-name-cell">
                <ElTag size="small" type="success">SQL</ElTag>
                <span>{{ row.fileName }}</span>
              </div>
            </template>
          </ElTableColumn>
          <ElTableColumn label="大小" width="120" align="right">
            <template #default="{ row }">{{ formatSize(row.sizeBytes) }}</template>
          </ElTableColumn>
          <ElTableColumn label="创建时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="完整路径" min-width="360" prop="filePath" />
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
  min-height: calc(100vh - 112px);
  padding: 20px;
}

.backup-header,
.backup-overview,
.backup-table-panel {
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.backup-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
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
  padding: 20px;
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
}
</style>
