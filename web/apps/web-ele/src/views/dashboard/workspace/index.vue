<script lang="ts" setup>
import type { AssetSummary, OverdueReportRow } from '#/api/report';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';

import {
  ElAlert,
  ElButton,
  ElCard,
  ElEmpty,
  ElSkeleton,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { listMyFlowsPageApi, listPendingFlowsPageApi } from '#/api/material';
import { getAssetSummaryApi, getOverdueReportApi } from '#/api/report';
import {
  getMineApprovalsPageApi,
  getPendingApprovalsPageApi,
  getPendingReturnsPageApi,
} from '#/api/workflow';
import { runHandled } from '#/utils/handled-promise';

import { combineAvailableCounts } from './approval-counts';
import { myApplicationPath, pendingApprovalPath } from './approval-navigation';
import { mergeSettledValue } from './dashboard-load-state';

defineOptions({ name: 'Workspace' });

const router = useRouter();
const { hasAccessByCodes } = useAccess();
const canViewAssets = computed(() => hasAccessByCodes(['asset:view']));
const canViewCategories = computed(() => hasAccessByCodes(['category:view']));
const canViewApprovals = computed(() => hasAccessByCodes(['approval:view']));
const canHandleApprovals = computed(() =>
  hasAccessByCodes(['approval:handle']),
);
const canViewMaterialFlows = computed(() =>
  hasAccessByCodes(['material-flow:view']),
);
const canHandleMaterialFlows = computed(() =>
  hasAccessByCodes(['material-flow:approve']),
);
const canViewAnyApplications = computed(
  () => canViewApprovals.value || canViewMaterialFlows.value,
);
const canHandleAnyApprovals = computed(
  () => canHandleApprovals.value || canHandleMaterialFlows.value,
);
const approvalNavigationAccess = computed(() => ({
  canHandleApprovals: canHandleApprovals.value,
  canHandleMaterialFlows: canHandleMaterialFlows.value,
  canViewApprovals: canViewApprovals.value,
  canViewMaterialFlows: canViewMaterialFlows.value,
}));
const pendingApprovalTarget = computed(() =>
  pendingApprovalPath(approvalNavigationAccess.value),
);
const myApplicationTarget = computed(() =>
  myApplicationPath(approvalNavigationAccess.value),
);
const canConfirmReturns = computed(() =>
  hasAccessByCodes(['approval:confirm-return']),
);
const canViewReports = computed(() => hasAccessByCodes(['report:view']));
const loading = ref(false);
const hasLoadedOnce = ref(false);
const summary = ref<AssetSummary | null>(null);
const overdueRows = ref<null | OverdueReportRow[]>(null);
const pendingApprovals = ref<null | number>(null);
const myPendingApprovals = ref<null | number>(null);
const pendingMaterialFlows = ref<null | number>(null);
const myPendingMaterialFlows = ref<null | number>(null);
const pendingReturns = ref<null | number>(null);

type DashboardLoadKey =
  | 'assetSummary'
  | 'materialMine'
  | 'materialPending'
  | 'mine'
  | 'overdue'
  | 'pending'
  | 'returns';

const loadErrors = ref<Partial<Record<DashboardLoadKey, null | string>>>({});
const loadErrorLabels: Record<DashboardLoadKey, string> = {
  assetSummary: '资产概况',
  materialMine: '我的料件申请',
  materialPending: '待审料件流转',
  mine: '我的资产申请',
  overdue: '逾期资产',
  pending: '待审资产申请',
  returns: '待确认归还',
};
const failedLoadLabels = computed(() =>
  (Object.keys(loadErrorLabels) as DashboardLoadKey[])
    .filter((key) => Boolean(loadErrors.value[key]))
    .map((key) => loadErrorLabels[key]),
);
const initialLoading = computed(() => loading.value && !hasLoadedOnce.value);

function formatCount(value: null | number) {
  return value ?? '—';
}

const seriousOverdueCount = computed(
  () => overdueRows.value?.filter((row) => row.isSerious).length ?? null,
);
const pendingMineCount = computed(() =>
  combineAvailableCounts([
    { enabled: canViewApprovals.value, value: myPendingApprovals.value },
    {
      enabled: canViewMaterialFlows.value,
      value: myPendingMaterialFlows.value,
    },
  ]),
);
const pendingApprovalCount = computed(() =>
  combineAvailableCounts([
    { enabled: canHandleApprovals.value, value: pendingApprovals.value },
    {
      enabled: canHandleMaterialFlows.value,
      value: pendingMaterialFlows.value,
    },
  ]),
);
const categoryTopRows = computed(
  () => summary.value?.byCategory.slice(0, 5) ?? [],
);

const metricCards = computed(() =>
  [
    {
      label: '资产总数',
      value: formatCount(summary.value?.total ?? null),
      tone: 'primary',
      path: '/asset/list',
      visible: canViewAssets.value && canViewReports.value,
    },
    {
      label: '在库资产',
      value: formatCount(summary.value?.available ?? null),
      tone: 'success',
      path: '/asset/list',
      visible: canViewAssets.value && canViewReports.value,
    },
    {
      label: '借出资产',
      value: formatCount(summary.value?.borrowed ?? null),
      tone: 'warning',
      path: '/report/borrow',
      visible: canViewReports.value,
    },
    {
      label: '逾期资产',
      value: formatCount(overdueRows.value?.length ?? null),
      tone: (overdueRows.value?.length ?? 0) > 0 ? 'danger' : 'success',
      path: '/report/overdue',
      visible: canViewReports.value,
    },
  ].filter((item) => item.visible),
);

const shortcuts = computed(() =>
  [
    { label: '资产列表', path: '/asset/list', visible: canViewAssets.value },
    {
      label: '资产分类',
      path: '/asset/categories',
      visible: canViewCategories.value,
    },
    {
      label: '待我审批',
      path: pendingApprovalTarget.value,
      visible: !!pendingApprovalTarget.value,
    },
    {
      label: '我的申请',
      path: myApplicationTarget.value,
      visible: !!myApplicationTarget.value,
    },
    {
      label: '资产汇总',
      path: '/report/summary',
      visible: canViewReports.value,
    },
  ].filter((item) => item.visible),
);

async function loadData() {
  if (loading.value) return;
  loading.value = true;
  try {
    const [
      assetSummary,
      overdue,
      pending,
      mine,
      returns,
      materialPending,
      materialMine,
    ] = await Promise.allSettled([
      canViewReports.value
        ? getAssetSummaryApi()
        : Promise.resolve(summary.value),
      canViewReports.value
        ? getOverdueReportApi()
        : Promise.resolve(overdueRows.value),
      canHandleApprovals.value
        ? getPendingApprovalsPageApi({ page: 1, pageSize: 1 }).then(
            (result) => result.total,
          )
        : Promise.resolve(pendingApprovals.value),
      canViewApprovals.value
        ? getMineApprovalsPageApi({
            page: 1,
            pageSize: 1,
            status: 'pending',
          }).then((result) => result.total)
        : Promise.resolve(myPendingApprovals.value),
      canConfirmReturns.value
        ? getPendingReturnsPageApi({ page: 1, pageSize: 1 }).then(
            (result) => result.total,
          )
        : Promise.resolve(pendingReturns.value),
      canHandleMaterialFlows.value
        ? listPendingFlowsPageApi({ page: 1, pageSize: 1 }).then(
            (result) => result.total,
          )
        : Promise.resolve(pendingMaterialFlows.value),
      canViewMaterialFlows.value
        ? listMyFlowsPageApi({ page: 1, pageSize: 1, status: 'pending' }).then(
            (result) => result.total,
          )
        : Promise.resolve(myPendingMaterialFlows.value),
    ]);
    const nextErrors = { ...loadErrors.value };
    if (canViewReports.value) {
      const nextSummary = mergeSettledValue(summary.value, assetSummary);
      summary.value = nextSummary.value;
      nextErrors.assetSummary = nextSummary.error;
      const nextOverdue = mergeSettledValue(overdueRows.value, overdue);
      overdueRows.value = nextOverdue.value;
      nextErrors.overdue = nextOverdue.error;
    }
    if (canHandleApprovals.value) {
      const nextPending = mergeSettledValue(pendingApprovals.value, pending);
      pendingApprovals.value = nextPending.value;
      nextErrors.pending = nextPending.error;
    }
    if (canViewApprovals.value) {
      const nextMine = mergeSettledValue(myPendingApprovals.value, mine);
      myPendingApprovals.value = nextMine.value;
      nextErrors.mine = nextMine.error;
    }
    if (canConfirmReturns.value) {
      const nextReturns = mergeSettledValue(pendingReturns.value, returns);
      pendingReturns.value = nextReturns.value;
      nextErrors.returns = nextReturns.error;
    }
    if (canHandleMaterialFlows.value) {
      const nextMaterialPending = mergeSettledValue(
        pendingMaterialFlows.value,
        materialPending,
      );
      pendingMaterialFlows.value = nextMaterialPending.value;
      nextErrors.materialPending = nextMaterialPending.error;
    }
    if (canViewMaterialFlows.value) {
      const nextMaterialMine = mergeSettledValue(
        myPendingMaterialFlows.value,
        materialMine,
      );
      myPendingMaterialFlows.value = nextMaterialMine.value;
      nextErrors.materialMine = nextMaterialMine.error;
    }
    loadErrors.value = nextErrors;
  } finally {
    hasLoadedOnce.value = true;
    loading.value = false;
  }
}

function go(path: null | string) {
  if (!path) return;
  runHandled(router.push(path));
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

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="workspace-container">
      <ElAlert
        v-if="failedLoadLabels.length > 0"
        :closable="false"
        show-icon
        type="warning"
      >
        <template #title>
          部分数据加载失败：{{ failedLoadLabels.join('、') }}
        </template>
        <div class="workspace-load-error">
          <span>已保留最近一次成功数据，未成功加载的指标显示为“—”。</span>
          <ElButton :loading="loading" link type="warning" @click="loadData">
            重新加载
          </ElButton>
        </div>
      </ElAlert>

      <ElSkeleton :loading="initialLoading" animated>
        <template #template>
          <div class="stat-cards">
            <div v-for="index in 4" :key="index" class="stat-card">
              <ElSkeleton :rows="2" animated />
            </div>
          </div>
        </template>

        <template #default>
          <section class="stat-cards workspace-stat-grid">
            <button
              v-for="item in metricCards"
              :key="item.label"
              :class="`workspace-stat-${item.tone}`"
              class="stat-card workspace-stat-card"
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
        <ElCard v-if="canViewReports" class="workspace-panel" shadow="never">
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
              <strong>{{ formatCount(seriousOverdueCount) }}</strong>
            </div>
          </div>
          <ElTable
            v-if="summary"
            :data="categoryTopRows"
            border
            class="workspace-table"
            style="margin-top: 16px"
          >
            <ElTableColumn label="分类" min-width="180">
              <template #default="{ row }">
                <button
                  class="category-code-link"
                  type="button"
                  @click="goCategoryAssets(row.categoryCode)"
                >
                  {{ row.categoryCode }}
                </button>
              </template>
            </ElTableColumn>
            <ElTableColumn
              align="center"
              label="总数"
              prop="total"
              width="90"
            />
            <ElTableColumn
              align="center"
              label="在库"
              prop="available"
              width="90"
            />
            <ElTableColumn
              align="center"
              label="借出"
              prop="borrowed"
              width="90"
            />
          </ElTable>
          <ElEmpty v-else description="资产概况暂不可用" />
        </ElCard>

        <div class="workspace-side">
          <ElCard class="workspace-panel workspace-todo-panel" shadow="never">
            <template #header>
              <div class="workspace-card-header">
                <span>待办提醒</span>
                <ElButton
                  v-if="canHandleApprovals"
                  link
                  type="primary"
                  @click="go('/approval/pending')"
                >
                  处理审批
                </ElButton>
              </div>
            </template>
            <div class="workspace-todo-list">
              <button
                v-if="canHandleAnyApprovals"
                class="workspace-todo-item"
                type="button"
                @click="go(pendingApprovalTarget)"
              >
                <span>待我审批</span>
                <strong>{{ formatCount(pendingApprovalCount) }}</strong>
              </button>
              <button
                v-if="canViewAnyApplications"
                class="workspace-todo-item"
                type="button"
                @click="go(myApplicationTarget)"
              >
                <span>我的审批中申请</span>
                <strong>{{ formatCount(pendingMineCount) }}</strong>
              </button>
              <button
                v-if="canConfirmReturns"
                class="workspace-todo-item"
                type="button"
                @click="go('/approval/confirm-return')"
              >
                <span>待确认归还</span>
                <strong>{{ formatCount(pendingReturns) }}</strong>
              </button>
              <button
                v-if="canViewReports"
                class="workspace-todo-item"
                type="button"
                @click="go('/report/overdue')"
              >
                <span>逾期未归还</span>
                <strong>{{ formatCount(overdueRows?.length ?? null) }}</strong>
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
                :key="item.label"
                class="workspace-shortcut-item"
                type="button"
                @click="go(item.path)"
              >
                {{ item.label }}
              </button>
            </div>
          </ElCard>
        </div>

        <ElCard v-if="canViewReports" class="workspace-panel" shadow="never">
          <template #header>
            <div class="workspace-card-header">
              <span>逾期资产</span>
              <ElButton link type="primary" @click="go('/report/overdue')">
                查看全部
              </ElButton>
            </div>
          </template>
          <ElTable
            v-if="overdueRows && overdueRows.length > 0"
            :data="overdueRows.slice(0, 5)"
            border
            class="workspace-table"
          >
            <ElTableColumn label="资产" min-width="180">
              <template #default="{ row }">
                <div>{{ row.assetName }}</div>
                <ElTag size="small">{{ row.assetNo }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="借用人" prop="borrower" width="100" />
            <ElTableColumn align="center" label="逾期天数" width="120">
              <template #default="{ row }">
                <ElTag
                  :type="row.isSerious ? 'danger' : 'warning'"
                  size="small"
                >
                  {{ row.overdueDays }} 天
                </ElTag>
              </template>
            </ElTableColumn>
          </ElTable>
          <ElEmpty v-else-if="overdueRows" description="暂无逾期资产" />
          <ElEmpty v-else description="逾期数据暂不可用" />
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

.workspace-load-error {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

/* 统计卡片特殊样式 */
.workspace-stat-card {
  cursor: pointer;
  border: 1px solid var(--asset-page-border);
  transition: all 0.2s ease;
}

.workspace-stat-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.workspace-stat-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 4px 12px var(--el-color-primary-light-7);
  transform: translateY(-2px);
}

.workspace-stat-primary .stat-value {
  color: var(--el-color-primary);
}

.workspace-stat-success .stat-value {
  color: var(--el-color-success);
}

.workspace-stat-warning .stat-value {
  color: var(--el-color-warning);
}

.workspace-stat-danger .stat-value {
  color: var(--el-color-danger);
}

/* 仪表板布局 */
.workspace-dashboard {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(320px, 0.8fr) minmax(0, 1fr);
  gap: 20px;
  max-height: calc(100vh - 240px);
}

.workspace-panel {
  display: flex;
  flex-direction: column;
  max-height: 100%;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

.workspace-panel :deep(.el-card__header) {
  flex-shrink: 0;
  padding: 16px 20px;
  background: var(--asset-page-panel-header);
  border-bottom: 1px solid var(--asset-page-border-strong);
}

.workspace-panel :deep(.el-card__body) {
  flex: 1;
  min-height: 0;
  padding: 20px;
  overflow-y: auto;
}

.workspace-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
  color: var(--asset-page-panel-header-text);
}

.workspace-card-header :deep(.el-button.is-link) {
  color: var(--asset-page-panel-header-text);
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
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
}

.workspace-summary-item span {
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.workspace-summary-item strong {
  display: block;
  margin-top: 8px;
  font-size: 24px;
  font-weight: 600;
  line-height: 32px;
  color: var(--asset-page-text);
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
  cursor: pointer;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  transition: all 0.2s ease;
}

.workspace-todo-item:hover {
  background: var(--asset-page-surface-soft);
  border-color: var(--el-color-primary);
}

.workspace-todo-item span {
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

.workspace-todo-item strong {
  font-size: 20px;
  font-weight: 600;
  line-height: 28px;
  color: var(--el-color-primary);
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
  font-size: 14px;
  line-height: 20px;
  color: var(--el-color-primary);
  cursor: pointer;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  transition: all 0.2s ease;
}

.workspace-shortcut-item:hover {
  background: var(--asset-page-surface-soft);
  border-color: var(--el-color-primary);
}

/* 表格样式 */
.workspace-table :deep(.el-table) {
  font-size: 14px;
  line-height: 20px;
}

.workspace-table :deep(.el-table th.el-table__cell) {
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  color: var(--asset-page-panel-header-text);
  background: var(--asset-page-panel-header-solid);
}

.workspace-table :deep(.el-table--border) {
  border: none;
}

.workspace-table :deep(.el-table td.el-table__cell),
.workspace-table :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.workspace-table :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

/* 响应式 */
/* stylelint-disable-next-line order/order -- 响应式覆盖必须位于基础规则之后 */
@media (max-width: 1280px) {
  .workspace-dashboard {
    grid-template-columns: repeat(2, 1fr);
  }

  .workspace-stat-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .workspace-dashboard > :last-child {
    grid-column: 1 / -1;
  }
}

@media (max-width: 768px) {
  .workspace-dashboard {
    grid-template-columns: 1fr;
    max-height: none;
    overflow: visible;
  }

  .workspace-stat-grid {
    grid-template-columns: 1fr;
  }

  .workspace-shortcuts {
    grid-template-columns: 1fr;
  }
}
</style>
