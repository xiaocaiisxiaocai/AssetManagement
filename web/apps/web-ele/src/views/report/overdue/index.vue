<script lang="ts" setup>
import type { OverdueReportRow } from '#/api/report';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import {
  ElButton,
  ElMessage,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  getOverdueReportApi,
  remindOverdueApi,
  remindOverdueBatchApi,
} from '#/api/report';
import { runHandled } from '#/utils/handled-promise';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildReportActionAccess } from '#/views/permissions/action-access';

defineOptions({ name: 'ReportOverdue' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const reportActionAccess = computed(() =>
  buildReportActionAccess(hasAccessByCodes),
);
const loading = ref(false);
const remindingId = ref<null | number>(null);
const rows = ref<OverdueReportRow[]>([]);
const selectedRows = ref<OverdueReportRow[]>([]);
const pageSizeOptions = ref(createPageSizeOptions(20));
const query = reactive({
  page: 1,
  pageSize: 20,
});
const seriousCount = computed(
  () => rows.value.filter((row) => row.isSerious).length,
);
const pagedRows = computed(() => {
  const start = (query.page - 1) * query.pageSize;
  return rows.value.slice(start, start + query.pageSize);
});

async function loadData() {
  loading.value = true;
  try {
    rows.value = await getOverdueReportApi();
    selectedRows.value = [];
    if ((query.page - 1) * query.pageSize >= rows.value.length) {
      query.page = 1;
    }
  } finally {
    loading.value = false;
  }
}

async function remind(row: OverdueReportRow) {
  remindingId.value = row.assetId;
  try {
    await remindOverdueApi(row.assetId);
    ElMessage.success('站内催办已记录');
  } finally {
    remindingId.value = null;
  }
}

async function remindBatch() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择逾期资产');
    return;
  }
  remindingId.value = -1;
  try {
    await remindOverdueBatchApi(selectedRows.value.map((row) => row.assetId));
    ElMessage.success('批量催办已记录');
  } finally {
    remindingId.value = null;
  }
}

function onSelectionChange(selection: OverdueReportRow[]) {
  selectedRows.value = selection;
}

function onPageSizeChange() {
  query.page = 1;
  selectedRows.value = [];
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
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">逾期资产报表</h2>
        </div>
        <div class="page-actions">
          <ElButton
            v-if="reportActionAccess.canRemind"
            :loading="remindingId === -1"
            type="warning"
            @click="remindBatch"
          >
            批量催办
          </ElButton>
        </div>
      </div>

      <div class="stat-cards report-stat-cards">
        <div class="stat-card">
          <div class="stat-label">逾期资产</div>
          <div class="stat-value stat-value-warning">{{ rows.length }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">严重逾期</div>
          <div class="stat-value stat-value-danger">{{ seriousCount }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">已选资产</div>
          <div class="stat-value">{{ selectedRows.length }}</div>
        </div>
      </div>

      <div class="table-panel">
        <ElTable
          :data="pagedRows"
          border
          height="100%"
          row-key="assetId"
          v-loading="loading"
          @selection-change="onSelectionChange"
        >
          <ElTableColumn
            v-if="reportActionAccess.canRemind"
            type="selection"
            width="48"
          />
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
            label="预计归还"
            min-width="120"
            prop="returnDate"
          />
          <ElTableColumn align="center" label="逾期天数" width="120">
            <template #default="{ row }">
              <ElTag :type="row.isSerious ? 'danger' : 'warning'" size="small">
                {{ row.overdueDays }} 天
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            v-if="reportActionAccess.canRemind"
            align="center"
            fixed="right"
            label="操作"
            width="110"
          >
            <template #default="{ row }">
              <ElButton
                :loading="remindingId === row.assetId"
                link
                size="small"
                type="warning"
                @click="remind(row)"
              >
                催办
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ rows.length }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="query.pageSize"
              style="width: 92px"
              @change="onPageSizeChange"
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
            :total="rows.length"
            background
            layout="prev, pager, next"
          />
        </div>
      </div>
    </div>
  </re-page>
</template>
