<script lang="ts" setup>
import type { BorrowReportQuery, BorrowReportRow } from '#/api/report';

import { onMounted, reactive, ref } from 'vue';

import { exportBorrowReportApi, getBorrowReportApi } from '#/api/report';

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

defineOptions({ name: 'ReportBorrow' });

const loading = ref(false);
const rows = ref<BorrowReportRow[]>([]);
const total = ref(0);
const query = reactive({
  borrowerId: undefined as number | undefined,
  categoryId: undefined as number | undefined,
  dateRange: [] as string[],
  page: 1,
  pageSize: 20,
  status: undefined as string | undefined,
});

async function loadData() {
  loading.value = true;
  try {
    const result = await getBorrowReportApi(buildQuery());
    rows.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function buildQuery(): BorrowReportQuery {
  return {
    borrowerId: query.borrowerId,
    categoryId: query.categoryId,
    endTime: query.dateRange[1],
    page: query.page,
    pageSize: query.pageSize,
    startTime: query.dateRange[0],
    status: query.status,
  };
}

function search() {
  query.page = 1;
  void loadData();
}

function resetQuery() {
  Object.assign(query, {
    borrowerId: undefined,
    categoryId: undefined,
    dateRange: [],
    page: 1,
    pageSize: 20,
    status: undefined,
  });
  void loadData();
}

async function exportReport() {
  const response = await exportBorrowReportApi(buildQuery());
  downloadBlob(response.data, 'borrowed-report.xlsx');
}

function statusText(status: string) {
  return status === 'returned' ? '已归还' : '借用中';
}

function statusType(status: string) {
  return status === 'returned' ? 'success' : 'warning';
}

function money(value: number) {
  return value.toLocaleString('zh-CN', {
    maximumFractionDigits: 2,
    minimumFractionDigits: 0,
  });
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
          <h2 class="text-lg font-semibold">借用明细</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            查看已通过借用流程形成的资产借用记录。
          </p>
        </div>
        <ElButton type="primary" @click="exportReport">导出</ElButton>
      </div>

      <div class="rounded border bg-card p-4">
        <ElForm inline>
          <ElFormItem label="申请时间">
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
          <ElFormItem label="借用人ID">
            <ElInput v-model.number="query.borrowerId" clearable placeholder="用户ID" />
          </ElFormItem>
          <ElFormItem label="分类ID">
            <ElInput v-model.number="query.categoryId" clearable placeholder="分类ID" />
          </ElFormItem>
          <ElFormItem label="状态">
            <ElSelect v-model="query.status" clearable placeholder="全部状态" style="width: 130px">
              <ElOption label="借用中" value="borrowed" />
              <ElOption label="已归还" value="returned" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <ElTable v-loading="loading" :data="rows" border>
        <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
        <ElTableColumn label="资产" min-width="220">
          <template #default="{ row }">
            <div>{{ row.assetName }}</div>
            <ElTag size="small">{{ row.assetNo }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="分类" min-width="170">
          <template #default="{ row }">
            <div>{{ row.categoryName || '-' }}</div>
            <ElTag v-if="row.categoryCode" size="small">{{ row.categoryCode }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="借用人" min-width="120" prop="borrower" />
        <ElTableColumn label="部门" min-width="120" prop="borrowerDept" />
        <ElTableColumn label="申请时间" min-width="160">
          <template #default="{ row }">{{ row.applyTime?.replace('T', ' ').slice(0, 16) }}</template>
        </ElTableColumn>
        <ElTableColumn label="预计归还" min-width="120" prop="returnDate" />
        <ElTableColumn label="金额" min-width="100">
          <template #default="{ row }">{{ money(row.amount) }}</template>
        </ElTableColumn>
        <ElTableColumn label="状态" width="100">
          <template #default="{ row }">
            <ElTag :type="statusType(row.status)">{{ statusText(row.status) }}</ElTag>
          </template>
        </ElTableColumn>
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
