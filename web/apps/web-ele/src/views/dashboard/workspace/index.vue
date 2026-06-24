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
    <div class="workspace-container">
      <ElSkeleton :loading="loading" animated>
        <template #template>
          <div class="stat-cards">
            <div v-for="index in 4" :key="index" class="stat-card">
              <ElSkeleton :rows="2" animated />
            </div>
          </div>
        </template>

        <template #default>
          <section class="stat-cards" style="grid-template-columns: repeat(4, 1fr);">
            <button
              v-for="item in metricCards"
              :key="item.label"
              class="stat-card workspace-stat-card"
              :class="`workspace-stat-${item.tone}`"
              type="button"
              @click="go(item.path)"
            >
              <div class="stat-label">{{ item.label }}</div>
              <div class="stat-value">{{ item.value }}</div>
            </button>
          </section>
        </template>
      </ElSkeleton>

      <section class="workspace-dashboard">
        <ElCard class="workspace-panel" shadow="never">
          <template #header>
            <div class="workspace-card-header">
              <span>资产概况</span>
              <ElButton link type="primary" @click="go('/report/summary')">
                查看汇总
              </ElButton>
            </div>
          </template>
          <div class="workspace-summary">
            <div class="workspace-summary-item">
              <span>严重逾期</span>
              <strong>{{ seriousOverdueCount }}</strong>
            </div>
          </div>
          <ElTable :data="categoryTopRows" class="workspace-table" border style="margin-top: 16px;">
            <ElTableColumn label="分类" min-width="180">
              <template #default="{ row }">
                <ElTag size="small">{{ row.categoryCode }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="总数" prop="total" width="90" align="center" />
            <ElTableColumn label="在库" prop="available" width="90" align="center" />
            <ElTableColumn label="借出" prop="borrowed" width="90" align="center" />
          </ElTable>
        </ElCard>

        <div class="workspace-side">
          <ElCard class="workspace-panel workspace-todo-panel" shadow="never">
            <template #header>
              <div class="workspace-card-header">
                <span>待办提醒</span>
                <ElButton link type="primary" @click="go('/approval/pending')">
                  处理审批
                </ElButton>
              </div>
            </template>
            <div class="workspace-todo-list">
              <button class="workspace-todo-item" type="button" @click="go('/approval/pending')">
                <span>待我审批</span>
                <strong>{{ pendingApprovals.length }}</strong>
              </button>
              <button class="workspace-todo-item" type="button" @click="go('/approval/mine')">
                <span>我的审批中申请</span>
                <strong>{{ pendingMineCount }}</strong>
              </button>
              <button class="workspace-todo-item" type="button" @click="go('/approval/confirm-return')">
                <span>待确认入库</span>
                <strong>{{ pendingReturns.length }}</strong>
              </button>
              <button class="workspace-todo-item" type="button" @click="go('/report/overdue')">
                <span>逾期未归还</span>
                <strong>{{ overdueRows.length }}</strong>
              </button>
            </div>
          </ElCard>

          <ElCard class="workspace-panel" shadow="never">
            <template #header>
              <div class="workspace-card-header">
                <span>快捷入口</span>
              </div>
            </template>
            <div class="workspace-shortcuts">
              <button
                v-for="item in shortcuts"
                :key="item.path"
                class="workspace-shortcut-item"
                type="button"
                @click="go(item.path)"
              >
                {{ item.label }}
              </button>
            </div>
          </ElCard>
        </div>

        <ElCard class="workspace-panel" shadow="never">
          <template #header>
            <div class="workspace-card-header">
              <span>逾期资产</span>
              <ElButton link type="primary" @click="go('/report/overdue')">
                查看全部
              </ElButton>
            </div>
          </template>
          <ElTable
            v-if="overdueRows.length"
            :data="overdueRows.slice(0, 5)"
            class="workspace-table"
            border
          >
            <ElTableColumn label="资产" min-width="180">
              <template #default="{ row }">
                <div>{{ row.assetName }}</div>
                <ElTag size="small">{{ row.assetNo }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="借用人" prop="borrower" width="100" />
            <ElTableColumn label="逾期天数" width="120" align="center">
              <template #default="{ row }">
                <ElTag :type="row.isSerious ? 'danger' : 'warning'" size="small">
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
.workspace-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}

/* 统计卡片特殊样式 */
.workspace-stat-card {
  cursor: pointer;
  transition: all 0.2s ease;
  border: 1px solid #e8e9eb;
}

.workspace-stat-card:hover {
  border-color: #3b82f6;
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.15);
  transform: translateY(-2px);
}

.workspace-stat-primary .stat-value {
  color: #3b82f6;
}

.workspace-stat-success .stat-value {
  color: #10b981;
}

.workspace-stat-warning .stat-value {
  color: #f59e0b;
}

.workspace-stat-danger .stat-value {
  color: #ef4444;
}

/* 仪表板布局 */
.workspace-dashboard {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(320px, 0.8fr) minmax(0, 1fr);
  gap: 20px;
  max-height: calc(100vh - 240px);
}

.workspace-panel {
  border: 1px solid #e8e9eb;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  max-height: 100%;
}

.workspace-panel :deep(.el-card__header) {
  padding: 16px 20px;
  border-bottom: 1px solid #e8e9eb;
  background: #f8f9fa;
  flex-shrink: 0;
}

.workspace-panel :deep(.el-card__body) {
  padding: 20px;
  overflow-y: auto;
  flex: 1;
  min-height: 0;
}

.workspace-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
  color: #1e293b;
}

/* 侧边栏布局 */
.workspace-side {
  display: grid;
  grid-template-rows: auto auto;
  gap: 20px;
}

/* 概况汇总 */
.workspace-summary {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
}

.workspace-summary-item {
  padding: 16px;
  border: 1px solid #e8e9eb;
  border-radius: 8px;
  background: #f8f9fa;
}

.workspace-summary-item span {
  font-size: 14px;
  line-height: 20px;
  color: #64748b;
}

.workspace-summary-item strong {
  display: block;
  margin-top: 8px;
  font-size: 24px;
  font-weight: 600;
  line-height: 32px;
  color: #1e293b;
}

/* 待办列表 */
.workspace-todo-list {
  display: grid;
  gap: 12px;
}

.workspace-todo-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 0 16px;
  border: 1px solid #e8e9eb;
  border-radius: 8px;
  background: #ffffff;
  cursor: pointer;
  transition: all 0.2s ease;
}

.workspace-todo-item:hover {
  border-color: #3b82f6;
  background: #f0f7ff;
}

.workspace-todo-item span {
  font-size: 14px;
  line-height: 20px;
  color: #475569;
}

.workspace-todo-item strong {
  font-size: 20px;
  font-weight: 600;
  line-height: 28px;
  color: #3b82f6;
}

/* 快捷入口 */
.workspace-shortcuts {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.workspace-shortcut-item {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0 16px;
  border: 1px solid #e8e9eb;
  border-radius: 8px;
  background: #ffffff;
  font-size: 14px;
  line-height: 20px;
  color: #3b82f6;
  cursor: pointer;
  transition: all 0.2s ease;
}

.workspace-shortcut-item:hover {
  border-color: #3b82f6;
  background: #f0f7ff;
}

/* 表格样式 */
.workspace-table :deep(.el-table) {
  font-size: 14px;
  line-height: 20px;
}

.workspace-table :deep(.el-table th.el-table__cell) {
  background: #f8f9fa;
  color: #475569;
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.workspace-table :deep(.el-table--border) {
  border: none;
}

.workspace-table :deep(.el-table td.el-table__cell),
.workspace-table :deep(.el-table th.el-table__cell) {
  border-color: #e8e9eb;
}

.workspace-table :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

/* 响应式 */
@media (max-width: 1280px) {
  .workspace-dashboard {
    grid-template-columns: repeat(2, 1fr);
  }

  .workspace-dashboard > :last-child {
    grid-column: 1 / -1;
  }
}

@media (max-width: 768px) {
  .workspace-dashboard {
    grid-template-columns: 1fr;
  }

  .workspace-shortcuts {
    grid-template-columns: 1fr;
  }
}
</style>
