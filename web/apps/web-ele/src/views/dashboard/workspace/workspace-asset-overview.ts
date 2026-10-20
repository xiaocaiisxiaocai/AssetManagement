import type {
  AssetSummary,
  OverdueReportPage,
  OverdueReportRow,
} from '#/api/report';

import type { ComputedRef, Ref } from 'vue';
import { computed, ref } from 'vue';

import { getAssetSummaryApi, getOverdueReportApi } from '#/api/report';

import { mergeSettledValue } from './dashboard-load-state';

export interface WorkspaceAssetOverviewState {
  overdueRows: Ref<null | OverdueReportRow[]>;
  overdueTotal: Ref<null | number>;
  summary: Ref<AssetSummary | null>;
}

export function createWorkspaceAssetOverview(access: {
  canViewAssets: ComputedRef<boolean>;
  canViewReports: ComputedRef<boolean>;
}) {
  const summary = ref<AssetSummary | null>(null);
  const overdueRows = ref<null | OverdueReportRow[]>(null);
  const overdueTotal = ref<null | number>(null);
  const categoryTopRows = computed(
    () => summary.value?.byCategory.slice(0, 5) ?? [],
  );

  function formatCount(value: null | number) {
    return value ?? '—';
  }

  const metricCards = computed(() =>
    [
      {
        label: '资产总数',
        value: formatCount(summary.value?.total ?? null),
        tone: 'primary',
        path: '/asset/list',
        visible: access.canViewAssets.value && access.canViewReports.value,
      },
      {
        label: '在库资产',
        value: formatCount(summary.value?.available ?? null),
        tone: 'success',
        path: '/asset/list',
        visible: access.canViewAssets.value && access.canViewReports.value,
      },
      {
        label: '借出资产',
        value: formatCount(summary.value?.borrowed ?? null),
        tone: 'warning',
        path: '/report/borrow',
        visible: access.canViewReports.value,
      },
      {
        label: '逾期资产',
        value: formatCount(overdueTotal.value),
        tone: (overdueTotal.value ?? 0) > 0 ? 'danger' : 'success',
        path: '/report/overdue',
        visible: access.canViewReports.value,
      },
    ].filter((item) => item.visible),
  );

  return {
    categoryTopRows,
    formatCount,
    metricCards,
    overdueRows,
    overdueTotal,
    summary,
  };
}

export function createWorkspaceAssetOverviewRequests(
  canViewReports: boolean,
  state: WorkspaceAssetOverviewState,
): {
  overdue: Promise<OverdueReportPage>;
  summary: Promise<AssetSummary | null>;
} {
  const summary: Promise<AssetSummary | null> = canViewReports
    ? getAssetSummaryApi()
    : Promise.resolve(state.summary.value);
  const overdue: Promise<OverdueReportPage> = canViewReports
    ? getOverdueReportApi({ page: 1, pageSize: 5 })
    : Promise.resolve({
        items: state.overdueRows.value ?? [],
        page: 1,
        pageSize: 5,
        seriousTotal: 0,
        total: state.overdueTotal.value ?? 0,
      });

  return { overdue, summary };
}

export function applyWorkspaceAssetOverviewResults(
  canViewReports: boolean,
  state: WorkspaceAssetOverviewState,
  assetSummary: PromiseSettledResult<AssetSummary | null>,
  overdue: PromiseSettledResult<OverdueReportPage>,
  nextErrors: {
    assetSummary?: null | string;
    overdue?: null | string;
  },
) {
  if (!canViewReports) return;

  const nextSummary = mergeSettledValue(state.summary.value, assetSummary);
  state.summary.value = nextSummary.value;
  nextErrors.assetSummary = nextSummary.error;
  const nextOverdue = mergeSettledValue<OverdueReportPage>(
    {
      items: state.overdueRows.value ?? [],
      page: 1,
      pageSize: 5,
      seriousTotal: 0,
      total: state.overdueTotal.value ?? 0,
    },
    overdue,
  );
  const nextOverdueValue = nextOverdue.value ?? {
    items: state.overdueRows.value ?? [],
    page: 1,
    pageSize: 5,
    seriousTotal: 0,
    total: state.overdueTotal.value ?? 0,
  };
  state.overdueRows.value = nextOverdueValue.items;
  state.overdueTotal.value = nextOverdueValue.total;
  nextErrors.overdue = nextOverdue.error;
}
