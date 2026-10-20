<script lang="ts" setup>
import type { DatabaseBackupFile } from '#/api/report';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { formatDateTime } from '#/utils/date-format';

defineProps<{
  canManage: boolean;
  loading: boolean;
  pageSizeOptions: number[];
  pagedRows: DatabaseBackupFile[];
  total: number;
}>();

const emit = defineEmits<{
  download: [row: DatabaseBackupFile];
  pageSizeChange: [];
}>();
const page = defineModel<number>('page', { required: true });
const pageSize = defineModel<number>('pageSize', { required: true });

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
</script>

<template>
  <div class="backup-table-panel page-fill table-scroll">
    <ElTable :data="pagedRows" border height="100%" v-loading="loading">
      <ElTableColumn label="文件名" min-width="260">
        <template #default="{ row }">
          <div class="file-name-cell">
            <ElTag :type="fileTypeTag(row.fileType)" size="small">
              {{ fileTypeLabel(row.fileType) }}
            </ElTag>
            <span>{{ row.fileName }}</span>
          </div>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="类型" width="110">
        <template #default="{ row }">
          {{ fileTypeLabel(row.fileType) }}
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="大小" width="120">
        <template #default="{ row }">
          {{ formatSize(row.sizeBytes) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="创建时间" width="180">
        <template #default="{ row }">
          {{ formatDateTime(row.createdAt, { seconds: true }) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="canManage"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="100"
      >
        <template #default="{ row }">
          <ElButton
            link
            size="small"
            type="primary"
            @click="emit('download', row)"
          >
            下载
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ total }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="pageSize"
          style="width: 92px"
          @change="emit('pageSizeChange')"
        >
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
        :total="total"
        background
        layout="prev, pager, next"
      />
    </div>
  </div>
</template>

<style scoped src="./BackupTable.css"></style>
