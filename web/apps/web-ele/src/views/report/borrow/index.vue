<script lang="ts" setup>
import type { CategoryNode } from '#/api/base-data';
import type { BorrowReportQuery, BorrowReportRow } from '#/api/report';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import {
  ElButton,
  ElDatePicker,
  ElForm,
  ElFormItem,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { getCategoryTreeApi } from '#/api/base-data';
import { exportBorrowReportApi, getBorrowReportApi } from '#/api/report';
import { getUserOptionsPageApi } from '#/api/user';
import { formatDateTime } from '#/utils/date-format';
import { endOfSelectedDay, startOfSelectedDay } from '#/utils/date-range';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { mergeUserOptions } from '#/utils/user-options';

defineOptions({ name: 'ReportBorrow' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const canExport = computed(() => hasAccessByCodes(['report:export']));
const listRequestGuard = createLatestRequestGuard();
const userOptionsLoading = ref(false);
const userOptionsRequestGuard = createLatestRequestGuard();
const loading = ref(false);
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
    borrowerOptions.value = mergeUserOptions(
      borrowerOptions.value,
      result.items,
    );
  } catch {
    // 请求层已提示，保留已回填选项。
  } finally {
    if (userOptionsRequestGuard.isLatest(requestGeneration))
      userOptionsLoading.value = false;
  }
}

async function exportReport() {
  const response = await exportBorrowReportApi(buildQuery());
  downloadBlob(response.data, '借用明细.xlsx');
}

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

function statusText(status: string) {
  return status === 'returned' ? '已归还' : '借用中';
}

function statusType(status: string) {
  return status === 'returned' ? 'success' : 'warning';
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
      <div class="filter-panel borrow-filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="申请时间">
            <ElDatePicker
              v-model="query.dateRange"
              end-placeholder="结束日期"
              format="YYYY-MM-DD"
              range-separator="至"
              start-placeholder="开始日期"
              style="width: 240px"
              type="daterange"
              value-format="YYYY-MM-DD"
            />
          </ElFormItem>
          <ElFormItem label="借用人">
            <ElSelect
              v-model="query.borrowerId"
              :loading="userOptionsLoading"
              :remote-method="searchBorrowers"
              aria-label="借用人"
              clearable
              filterable
              placeholder="选择借用人"
              remote
              style="width: 180px"
            >
              <ElOption
                v-for="user in borrowerOptions"
                :key="user.id"
                :label="`${user.name}（${user.employeeNo}）`"
                :value="user.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="分类">
            <ElSelect
              v-model="query.categoryId"
              aria-label="资产分类"
              clearable
              filterable
              placeholder="选择分类"
              style="width: 180px"
            >
              <ElOption
                v-for="category in categoryOptions"
                :key="category.id"
                :label="category.code"
                :value="category.id"
              />
            </ElSelect>
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
        <div v-if="canExport" class="borrow-filter-actions">
          <ElButton type="primary" @click="exportReport">导出 Excel</ElButton>
        </div>
      </div>

      <div class="table-panel-with-toolbar">
        <ElTable :data="rows" border height="100%" v-loading="loading">
          <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
          <ElTableColumn label="资产" min-width="220">
            <template #default="{ row }">
              <div>{{ row.assetName }}</div>
              <ElTag size="small">{{ row.assetNo }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            class-name="hide-on-mobile"
            label="分类"
            min-width="170"
          >
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
          <ElTableColumn
            class-name="hide-on-mobile"
            label="部门"
            min-width="120"
            prop="borrowerDept"
          />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="申请时间"
            min-width="160"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.applyTime) }}
            </template>
          </ElTableColumn>
          <ElTableColumn
            class-name="hide-on-mobile"
            label="预计归还"
            min-width="120"
            prop="returnDate"
          />
          <ElTableColumn align="center" label="状态" width="100">
            <template #default="{ row }">
              <ElTag :type="statusType(row.status)" size="small">
                {{ statusText(row.status) }}
              </ElTag>
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

<style scoped>
.borrow-filter-panel {
  display: flex;
  gap: 16px;
  align-items: center;
}

.borrow-filter-panel .filter-form {
  flex: 1;
  min-width: 0;
}

.borrow-filter-actions {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  margin-left: auto;
}

@media (max-width: 768px) {
  .borrow-filter-panel {
    flex-wrap: wrap;
  }

  .borrow-filter-panel .filter-form {
    flex-basis: 100%;
  }

  .borrow-filter-actions {
    width: 100%;
    justify-content: flex-end;
  }
}
</style>
