<script lang="ts" setup>
import type { AuditLogQuery, AuditLogRow } from '#/api/report';

import { onMounted, reactive, ref } from 'vue';

import { exportAuditLogsApi, getAuditLogsApi } from '#/api/report';

import {
  ElButton,
  ElDatePicker,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'AdminAudit' });

type TagType = 'danger' | 'info' | 'success' | 'warning';

const loading = ref(false);
const rows = ref<AuditLogRow[]>([]);
const total = ref(0);
const query = reactive({
  actionType: '',
  dateRange: [] as string[],
  module: '',
  page: 1,
  pageSize: 20,
  userId: undefined as number | undefined,
});

async function loadData() {
  loading.value = true;
  try {
    const result = await getAuditLogsApi(buildQuery());
    rows.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function buildQuery(): AuditLogQuery {
  return {
    actionType: query.actionType || undefined,
    endTime: query.dateRange[1],
    module: query.module || undefined,
    page: query.page,
    pageSize: query.pageSize,
    startTime: query.dateRange[0],
    userId: query.userId,
  };
}

function search() {
  query.page = 1;
  void loadData();
}

function resetQuery() {
  Object.assign(query, {
    actionType: '',
    dateRange: [],
    module: '',
    page: 1,
    pageSize: 20,
    userId: undefined,
  });
  void loadData();
}

async function exportReport() {
  const response = await exportAuditLogsApi(buildQuery());
  downloadBlob(response.data, 'audit-logs.xlsx');
}

function actionType(type: string): TagType | undefined {
  if (type === 'remind') return 'warning';
  if (type === 'DELETE') return 'danger';
  if (type === 'POST') return 'success';
  if (type === 'PUT') return 'info';
  return undefined;
}

function formatTime(value: string) {
  return value?.replace('T', ' ').slice(0, 19);
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
          <h2 class="text-lg font-semibold">审计日志</h2>
        </div>
        <ElButton type="primary" @click="exportReport">导出</ElButton>
      </div>

      <div class="rounded border bg-card p-4">
        <ElForm inline>
          <ElFormItem label="时间">
            <ElDatePicker
              v-model="query.dateRange"
              end-placeholder="结束日期"
              format="YYYY-MM-DD"
              range-separator="至"
              start-placeholder="开始日期"
              type="daterange"
              value-format="YYYY-MM-DD"
            />
          </ElFormItem>
          <ElFormItem label="操作">
            <ElSelect v-model="query.actionType" clearable placeholder="全部操作" style="width: 130px">
              <ElOption label="新增" value="POST" />
              <ElOption label="修改" value="PUT" />
              <ElOption label="删除" value="DELETE" />
              <ElOption label="催办" value="remind" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="模块">
            <ElInput v-model="query.module" clearable placeholder="模块/摘要关键字" />
          </ElFormItem>
          <ElFormItem label="用户ID">
            <ElInput v-model.number="query.userId" clearable placeholder="用户ID" />
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <ElTable v-loading="loading" :data="rows" border>
        <ElTableColumn label="时间" min-width="170">
          <template #default="{ row }">{{ formatTime(row.occurredAt) }}</template>
        </ElTableColumn>
        <ElTableColumn label="操作人" min-width="120">
          <template #default="{ row }">{{ row.userName || row.userId || '-' }}</template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="100">
          <template #default="{ row }">
            <ElTag :type="actionType(row.actionType)">{{ row.actionType }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="模块" min-width="120" prop="targetType" />
        <ElTableColumn label="目标ID" min-width="100" prop="targetId" />
        <ElTableColumn label="摘要" min-width="260" prop="summary" />
        <ElTableColumn label="IP" min-width="140" prop="ip" />
      </ElTable>

      <div class="flex justify-end">
        <ElPagination
          v-model:current-page="query.page"
          v-model:page-size="query.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="total"
          background
          layout="total, sizes, prev, pager, next"
          @current-change="loadData"
          @size-change="search"
        />
      </div>
    </div>
  </re-page>
</template>
