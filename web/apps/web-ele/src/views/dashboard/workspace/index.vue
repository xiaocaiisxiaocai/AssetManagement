<script lang="ts" setup>
import type { AssetSummary, OverdueReportRow } from '#/api/report';
import type { ApprovalFlow } from '#/api/workflow';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { getAssetSummaryApi, getOverdueReportApi } from '#/api/report';
import {
  getMineApprovalsApi,
  getPendingApprovalsApi,
  getPendingReturnsApi,
} from '#/api/workflow';

import {
  ElButton,
  ElCard,
  ElEmpty,
  ElSkeleton,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'Workspace' });

const router = useRouter();
const loading = ref(false);
const summary = ref<AssetSummary>({
  available: 0,
  borrowed: 0,
  byCategory: [],
  byDept: [],
  total: 0,
});
const overdueRows = ref<OverdueReportRow[]>([]);
const pendingApprovals = ref<ApprovalFlow[]>([]);
const myApprovals = ref<ApprovalFlow[]>([]);
const pendingReturns = ref<ApprovalFlow[]>([]);

const seriousOverdueCount = computed(
  () => overdueRows.value.filter((row) => row.isSerious).length,
);
const pendingMineCount = computed(
  () => myApprovals.value.filter((row) => row.status === 'pending').length,
);
const categoryTopRows = computed(() => summary.value.byCategory.slice(0, 5));

const metricCards = computed(() => [
  {
    label: '资产总数',
    value: summary.value.total,
    tone: 'primary',
    path: '/asset/list',
  },
  {
    label: '在库资产',
    value: summary.value.available,
    tone: 'success',
    path: '/asset/list',
  },
  {
    label: '借出资产',
    value: summary.value.borrowed,
    tone: 'warning',
    path: '/report/borrow',
  },
  {
    label: '逾期资产',
    value: overdueRows.value.length,
    tone: overdueRows.value.length > 0 ? 'danger' : 'success',
    path: '/report/overdue',
  },
]);

const shortcuts = [
  { label: '资产列表', path: '/asset/list' },
  { label: '资产分类', path: '/asset/categories' },
  { label: '存放位置', path: '/asset/locations' },
  { label: '待我审批', path: '/approval/pending' },
  { label: '我的申请', path: '/approval/mine' },
  { label: '资产汇总', path: '/report/summary' },
];

async function loadData() {
  loading.value = true;
  try {
    const [assetSummary, overdue, pending, mine, returns] = await Promise.all([
      getAssetSummaryApi().catch(() => summary.value),
      getOverdueReportApi().catch(() => []),
      getPendingApprovalsApi().catch(() => []),
      getMineApprovalsApi().catch(() => []),
      getPendingReturnsApi().catch(() => []),
    ]);
    summary.value = assetSummary;
    overdueRows.value = overdue;
    pendingApprovals.value = pending;
    myApprovals.value = mine;
    pendingReturns.value = returns;
  } finally {
    loading.value = false;
  }
}

function go(path: string) {
  router.push(path);
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="home-page p-5">
      <section class="home-header">
        <div>
          <h2>首页</h2>
        </div>
        <div class="home-header-actions">
          <ElButton @click="loadData">刷新</ElButton>
          <ElButton type="primary" @click="go('/asset/list')">进入资产列表</ElButton>
        </div>
      </section>

      <ElSkeleton :loading="loading" animated>
        <template #template>
          <div class="home-grid">
            <div v-for="index in 4" :key="index" class="home-card">
              <ElSkeleton :rows="2" animated />
            </div>
          </div>
        </template>

        <template #default>
          <section class="home-grid">
            <button
              v-for="item in metricCards"
              :key="item.label"
              class="metric-card"
              :class="`metric-${item.tone}`"
              type="button"
              @click="go(item.path)"
            >
              <span>{{ item.label }}</span>
              <strong>{{ item.value }}</strong>
            </button>
          </section>
        </template>
      </ElSkeleton>

      <section class="home-dashboard">
        <ElCard class="home-panel" shadow="never">
          <template #header>
            <div class="card-head">
              <span>资产概况</span>
              <ElButton link type="primary" @click="go('/report/summary')">
                查看汇总
              </ElButton>
            </div>
          </template>
          <div class="summary-strip">
            <div>
              <span>严重逾期</span>
              <strong>{{ seriousOverdueCount }}</strong>
            </div>
          </div>
          <ElTable :data="categoryTopRows" class="home-table mt-3" border>
            <ElTableColumn label="分类" min-width="180">
              <template #default="{ row }">
                <ElTag size="small">{{ row.categoryCode }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="总数" prop="total" width="90" />
            <ElTableColumn label="在库" prop="available" width="90" />
            <ElTableColumn label="借出" prop="borrowed" width="90" />
          </ElTable>
        </ElCard>

        <div class="home-side">
          <ElCard class="home-panel home-todo-panel" shadow="never">
            <template #header>
              <div class="card-head">
                <span>待办提醒</span>
                <ElButton link type="primary" @click="go('/approval/pending')">
                  处理审批
                </ElButton>
              </div>
            </template>
            <div class="todo-list">
              <button type="button" @click="go('/approval/pending')">
                <span>待我审批</span>
                <strong>{{ pendingApprovals.length }}</strong>
              </button>
              <button type="button" @click="go('/approval/mine')">
                <span>我的审批中申请</span>
                <strong>{{ pendingMineCount }}</strong>
              </button>
              <button type="button" @click="go('/approval/confirm-return')">
                <span>待确认入库</span>
                <strong>{{ pendingReturns.length }}</strong>
              </button>
              <button type="button" @click="go('/report/overdue')">
                <span>逾期未归还</span>
                <strong>{{ overdueRows.length }}</strong>
              </button>
            </div>
          </ElCard>

          <ElCard class="home-panel" shadow="never">
            <template #header>
              <div class="card-head">
                <span>快捷入口</span>
              </div>
            </template>
            <div class="shortcut-grid">
              <button
                v-for="item in shortcuts"
                :key="item.path"
                type="button"
                @click="go(item.path)"
              >
                {{ item.label }}
              </button>
            </div>
          </ElCard>
        </div>

        <ElCard class="home-panel" shadow="never">
          <template #header>
            <div class="card-head">
              <span>逾期资产</span>
              <ElButton link type="primary" @click="go('/report/overdue')">
                查看全部
              </ElButton>
            </div>
          </template>
          <ElTable
            v-if="overdueRows.length"
            :data="overdueRows.slice(0, 5)"
            class="home-table"
            border
          >
            <ElTableColumn label="资产" min-width="180">
              <template #default="{ row }">
                <div>{{ row.assetName }}</div>
                <ElTag size="small">{{ row.assetNo }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="借用人" prop="borrower" width="100" />
            <ElTableColumn label="逾期天数" width="100">
              <template #default="{ row }">
                <ElTag :type="row.isSerious ? 'danger' : 'warning'">
                  {{ row.overdueDays }} 天
                </ElTag>
              </template>
            </ElTableColumn>
          </ElTable>
          <ElEmpty v-else description="暂无逾期资产" />
        </ElCard>
      </section>
    </div>
  </re-page>
</template>

<style scoped>
.home-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: calc(100vh - 112px);
  min-height: 0;
  overflow: hidden;
}

.home-header,
.home-card,
.metric-card {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-bg-color);
}

.home-header {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  flex-shrink: 0;
  padding: 14px 18px;
}

.home-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
}

.home-header-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.home-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  flex-shrink: 0;
  gap: 12px;
}

.home-card,
.metric-card {
  min-height: 86px;
  padding: 14px 16px;
}

.metric-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: flex-start;
  cursor: pointer;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.metric-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: var(--el-box-shadow-light);
}

.metric-card span {
  color: var(--el-text-color-secondary);
}

.metric-card strong {
  font-size: 26px;
  line-height: 1;
}

.metric-primary strong {
  color: var(--el-color-primary);
}

.metric-success strong {
  color: var(--el-color-success);
}

.metric-warning strong {
  color: var(--el-color-warning);
}

.metric-danger strong {
  color: var(--el-color-danger);
}

.home-dashboard {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(320px, 0.75fr) minmax(0, 1fr);
  gap: 12px;
  min-height: 0;
  overflow: hidden;
}

.home-panel {
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.home-panel :deep(.el-card__body) {
  min-height: 0;
  overflow: auto;
}

.home-side {
  display: grid;
  grid-template-rows: minmax(0, 1fr) auto;
  gap: 12px;
  min-height: 0;
  overflow: hidden;
}

.home-todo-panel :deep(.el-card__body) {
  overflow: hidden;
}

.home-table {
  max-height: 260px;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-weight: 600;
}

.summary-strip {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 8px;
}

.summary-strip > div,
.todo-list button,
.shortcut-grid button {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-fill-color-lighter);
}

.summary-strip > div {
  padding: 10px 12px;
}

.summary-strip span,
.todo-list span {
  color: var(--el-text-color-secondary);
}

.summary-strip strong {
  display: block;
  margin-top: 4px;
  font-size: 16px;
}

.todo-list {
  display: grid;
  gap: 8px;
}

.todo-list button,
.shortcut-grid button {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 42px;
  padding: 0 14px;
  cursor: pointer;
}

.todo-list strong {
  font-size: 20px;
  color: var(--el-color-primary);
}

.shortcut-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.shortcut-grid button {
  justify-content: center;
  color: var(--el-color-primary);
}

@media (max-width: 1100px) {
  .home-grid,
  .home-dashboard,
  .summary-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .home-dashboard {
    overflow: auto;
  }
}

@media (max-width: 720px) {
  .home-grid,
  .home-dashboard,
  .summary-strip,
  .shortcut-grid {
    grid-template-columns: 1fr;
  }

  .home-page {
    height: auto;
    overflow: visible;
  }
}
</style>
