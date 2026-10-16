<script lang="ts" setup>
import type { DatabaseBackupFile } from '#/api/report';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  backupDatabaseApi,
  downloadDatabaseBackupApi,
  getDatabaseBackupsApi,
} from '#/api/report';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';

import BackupHeader from './BackupHeader.vue';
import BackupTable from './BackupTable.vue';

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
    ElMessage.success(`备份完成：${result.fileName}`);
    await loadData();
  } finally {
    backupLoading.value = false;
  }
}

async function downloadBackup(row: DatabaseBackupFile) {
  const response = await downloadDatabaseBackupApi(row.fileName);
  downloadBlob(response.data, row.fileName);
}

function onPageSizeChange() {
  page.value = 1;
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();
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
    <div class="backup-page page-fit">
      <BackupHeader
        :backup-loading="backupLoading"
        :can-manage="canManageBackup"
        @backup="backupDatabase"
      />
      <BackupTable
        v-model:page="page"
        v-model:page-size="pageSize"
        :can-manage="canManageBackup"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :paged-rows="pagedRows"
        :total="rows.length"
        @download="downloadBackup"
        @page-size-change="onPageSizeChange"
      />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
