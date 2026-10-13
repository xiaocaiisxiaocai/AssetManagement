<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';
import type { BorrowReportQuery, BorrowReportRow } from '#/api/report';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import { getCategoryTreeApi } from '#/api/base-data';
import { exportBorrowReportApi, getBorrowReportApi } from '#/api/report';
import { getUserOptionsPageApi } from '#/api/user';
import { endOfSelectedDay, startOfSelectedDay } from '#/utils/date-range';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { replaceUserOptions } from '#/utils/user-options';
import { buildReportActionAccess } from '#/views/permissions/action-access';

import BorrowFilter from './BorrowFilter.vue';
import BorrowTable from './BorrowTable.vue';

defineOptions({ name: 'ReportBorrow' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const reportActionAccess = computed(() =>
  buildReportActionAccess(hasAccessByCodes),
);
const listRequestGuard = createLatestRequestGuard();
const userOptionsLoading = ref(false);
const userOptionsRequestGuard = createLatestRequestGuard();
const loading = ref(false);
const exporting = ref(false);
const rows = ref<BorrowReportRow[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));
const borrowerOptions = ref<UserOptionDto[]>([]);
const categoryOptions = ref<CategoryNode[]>([]);
const query = reactive({
  borrowerId: undefined as number | undefined,
  categoryId: undefined as number | undefined,
  dateRange: [] as string[],
  page: 1,
  pageSize: 20,
  status: undefined as string | undefined,
});
const queryModel = reactiveObjectModel(query);

async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  try {
    const result = await getBorrowReportApi(buildQuery());
    if (!listRequestGuard.isLatest(requestGeneration)) return;
    rows.value = result.items;
    total.value = result.total;
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

function buildQuery(): BorrowReportQuery {
  return {
    borrowerId: query.borrowerId,
    categoryId: query.categoryId,
    endTime: endOfSelectedDay(query.dateRange[1]),
    page: query.page,
    pageSize: query.pageSize,
    startTime: startOfSelectedDay(query.dateRange[0]),
    status: query.status,
  };
}

async function loadFilterOptions() {
  const [users, categories] = await Promise.allSettled([
    hasAccessByCodes(['report:view'])
      ? getUserOptionsPageApi().then((result) => result.items)
      : Promise.resolve([]),
    hasAccessByCodes(['category:view'])
      ? getCategoryTreeApi()
      : Promise.resolve([]),
  ]);
  if (users.status === 'fulfilled') borrowerOptions.value = users.value;
  if (categories.status === 'fulfilled')
    categoryOptions.value = flattenCategories(categories.value);
}

async function searchBorrowers(keyword = '') {
  if (!hasAccessByCodes(['report:view'])) return;
  const requestGeneration = userOptionsRequestGuard.next();
  userOptionsLoading.value = true;
  try {
    const result = await getUserOptionsPageApi(keyword, 1, 50);
    if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
    borrowerOptions.value = replaceUserOptions(
      borrowerOptions.value,
      result.items,
      [query.borrowerId],
    );
  } catch {
    // 请求层已提示，保留已回填选项。
  } finally {
    if (userOptionsRequestGuard.isLatest(requestGeneration))
      userOptionsLoading.value = false;
  }
}

/** 筛选可选任意层级；后端按分类编码前缀包含下级分类。 */
function flattenCategories(nodes: CategoryNode[]): CategoryNode[] {
  return nodes.flatMap((node) => [
    node,
    ...flattenCategories(node.children ?? []),
  ]);
}

function search() {
  query.page = 1;
  runHandled(loadData());
}

async function exportReport() {
  if (exporting.value) return;
  exporting.value = true;
  try {
    const response = await exportBorrowReportApi(buildQuery());
    downloadBlob(response.data, '借用明细.xlsx');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    exporting.value = false;
  }
}

function resetQuery() {
  Object.assign(query, {
    borrowerId: undefined,
    categoryId: undefined,
    dateRange: [],
    page: 1,
    status: undefined,
  });
  runHandled(loadData());
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
  await loadFilterOptions();
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <BorrowFilter
        v-model:query="queryModel"
        :borrower-options="borrowerOptions"
        :can-export="reportActionAccess.canExport"
        :category-options="categoryOptions"
        :exporting="exporting"
        :user-options-loading="userOptionsLoading"
        @export="exportReport"
        @reset="resetQuery"
        @search="search"
        @user-search="searchBorrowers"
      />
      <BorrowTable
        v-model:query="queryModel"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :rows="rows"
        :total="total"
        @category-click="goCategoryAssets"
        @page-change="loadData"
        @page-size-change="search"
      />
    </div>
  </re-page>
</template>
