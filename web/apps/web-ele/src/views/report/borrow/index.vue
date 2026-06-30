<script lang="ts" setup>
import type { BorrowReportQuery, BorrowReportRow } from '#/api/report';

import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { exportBorrowReportApi, getBorrowReportApi } from '#/api/report';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';

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

const router = useRouter();
const loading = ref(false);
const rows = ref<BorrowReportRow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));
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

function goCategoryAssets(categoryCode: string) {
  if (!categoryCode) return;
  router.push({
    path: '/asset/list',
    query: { categoryCode },
  });
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
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">借用明细报表</h2>
          <p class="page-subtitle">资产借用记录查询与导出</p>
        </div>
        <ElButton type="primary" @click="exportReport">导出</ElButton>
      </div>

      <div class="filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="申请时间">
            <ElDatePicker
              v-model="query.dateRange"
              end-placeholder="结束日期"
              format="YYYY-MM-DD"
              range-separator="至"
              start-placeholder="开始日期"
              type="daterange"
              value-format="YYYY-MM-DD"
              style="width: 240px"
            />
          </ElFormItem>
          <ElFormItem label="借用人ID">
            <ElInput v-model.number="query.borrowerId" clearable placeholder="用户ID" style="width: 140px" />
          </ElFormItem>
          <ElFormItem label="分类ID">
            <ElInput v-model.number="query.categoryId" clearable placeholder="分类ID" style="width: 140px" />
          </ElFormItem>
          <ElFormItem label="状态">
            <ElSelect
              v-model="query.status"
              aria-label="借用状态"
              clearable
              placeholder="全部状态"
              style="width: 130px"
            >
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

      <div class="table-panel-with-toolbar">
        <ElTable v-loading="loading" :data="rows" border height="100%">
          <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
          <ElTableColumn label="资产" min-width="220">
            <template #default="{ row }">
              <div>{{ row.assetName }}</div>
              <ElTag size="small">{{ row.assetNo }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="分类" min-width="170">
            <template #default="{ row }">
              <button
                v-if="row.categoryCode"
                class="category-code-link"
                type="button"
                @click="goCategoryAssets(row.categoryCode)"
              >
                {{ row.categoryCode }}
              </button>
              <span v-else class="empty-text">-</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="借用人" min-width="120" prop="borrower" />
          <ElTableColumn class-name="hide-on-mobile" label="部门" min-width="120" prop="borrowerDept" />
          <ElTableColumn class-name="hide-on-mobile" label="申请时间" min-width="160">
            <template #default="{ row }">{{ row.applyTime?.replace('T', ' ').slice(0, 16) }}</template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="预计归还" min-width="120" prop="returnDate" />
          <ElTableColumn label="状态" width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="statusType(row.status)" size="small">{{ statusText(row.status) }}</ElTag>
            </template>
          </ElTableColumn>
        </ElTable>

        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ total }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="query.pageSize"
              aria-label="借用明细每页条数"
              style="width: 92px"
              @change="search"
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
            v-model:current-page="query.page"
            :page-size="query.pageSize"
            :total="total"
            background
            layout="prev, pager, next"
            @current-change="loadData"
          />
        </div>
      </div>
    </div>
  </re-page>
</template>
