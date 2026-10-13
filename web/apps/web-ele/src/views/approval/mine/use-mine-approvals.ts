import type { ApprovalWorkItem } from '../approval-work-items';

import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { selectableTransferees } from '#/utils/user-options';
import { buildApprovalActionAccess } from '#/views/permissions/action-access';

import {
  type ApprovalSource,
  notificationSource,
} from '../notification-flow-attempt';
import { getApprovalAssetSelectCopy } from './approval-asset-options';
import { bindMineListActions } from './mine-list-actions';
import { bindMineStartActions } from './mine-start-actions';

export function useMineApprovals() {
  const route = useRoute();
  const router = useRouter();
  const userStore = useUserStore();
  const { hasAccessByCodes } = useAccess();
  const approvalActionAccess = computed(() =>
    buildApprovalActionAccess(hasAccessByCodes),
  );
  const canViewMaterialFlow = computed(() =>
    hasAccessByCodes(['material-flow:view']),
  );
  const loading = ref(false);
  const saving = ref(false);
  const withdrawingKeys = ref<string[]>([]);
  const dialogVisible = ref(false);
  const progressVisible = ref(false);
  const selectedFlow = ref<ApprovalWorkItem>();
  const openedNotificationFlowKey = ref('');
  const listRequestGuard = createLatestRequestGuard();
  const activeSource = ref<ApprovalSource>('asset');
  const assetFlows = ref<ApprovalWorkItem[]>([]);
  const materialFlows = ref<ApprovalWorkItem[]>([]);
  const assetTotal = ref(0);
  const materialTotal = ref(0);
  const assets = ref<AssetItem[]>([]);
  const assetOptionsLoading = ref(false);
  const assetSearchKeyword = ref('');
  const assetOptionsRequestGuard = createLatestRequestGuard();
  const users = ref<UserOptionDto[]>([]);
  const userOptionsLoading = ref(false);
  const userOptionsRequestGuard = createLatestRequestGuard();
  const pageSizeOptions = ref(createPageSizeOptions(20));
  const form = reactive({
    assetId: undefined as number | undefined,
    bizType: 'borrow',
    reason: '',
    returnDate: '',
    transfereeId: undefined as number | undefined,
  });
  const formModel = reactiveObjectModel(form);
  const query = reactive({
    bizType: '',
    keyword: '',
    status: '',
  });
  const queryModel = reactiveObjectModel(query);
  const showReturnDate = computed(() =>
    ['borrow', 'extension'].includes(form.bizType),
  );
  const assetSelectCopy = computed(() =>
    getApprovalAssetSelectCopy(form.bizType, assetSearchKeyword.value),
  );
  const selectedAsset = computed(() =>
    assets.value.find((asset) => asset.id === form.assetId),
  );
  const transfereeOptions = computed(() =>
    selectableTransferees(users.value, [
      Number(userStore.userInfo?.userId || 0),
      selectedAsset.value?.custodianId,
    ]),
  );
  const inheritedReturnDate = computed(
    () => selectedAsset.value?.returnDate || '无（当前资产未借出）',
  );

  const assetPage = reactive({ page: 1, pageSize: 20 });
  const materialPage = reactive({ page: 1, pageSize: 20 });
  const currentPage = computed(() =>
    activeSource.value === 'asset' ? assetPage : materialPage,
  );
  const currentPageModel = computed({
    get: () => currentPage.value,
    set: (value) => Object.assign(currentPage.value, value),
  });
  const flows = computed(() =>
    activeSource.value === 'asset' ? assetFlows.value : materialFlows.value,
  );
  const currentTotal = computed(() =>
    activeSource.value === 'asset' ? assetTotal.value : materialTotal.value,
  );

  const bindMineListActionsResult = bindMineListActions({
    route,
    router,
    canViewMaterialFlow,
    loading,
    withdrawingKeys,
    progressVisible,
    selectedFlow,
    openedNotificationFlowKey,
    listRequestGuard,
    activeSource,
    assetFlows,
    materialFlows,
    assetTotal,
    materialTotal,
    query,
    currentPage,
    flows,
  });
  const {
    loadData,
    clearNotificationFlowQuery,
    withdraw,
    onPageSizeChange,
    search,
    resetQuery,
    onSourceChange,
    showProgress,
    focusNotificationFlow,
  } = bindMineListActionsResult;

  watch(
    () => [route.query.flowId, route.query.source],
    () => {
      activeSource.value = notificationSource(route.query.source, route.path);
      currentPage.value.page = 1;
      runHandled(loadData());
    },
  );

  const bindMineStartActionsResult = bindMineStartActions({
    userStore,
    saving,
    dialogVisible,
    activeSource,
    assets,
    assetOptionsLoading,
    assetSearchKeyword,
    assetOptionsRequestGuard,
    users,
    userOptionsLoading,
    userOptionsRequestGuard,
    form,
    showReturnDate,
    selectedAsset,
    assetPage,
    loadData,
  });
  const {
    disableReturnDate,
    openStart,
    searchAssets,
    searchUsers,
    onBizTypeChange,
    onAssetChange,
    submit,
  } = bindMineStartActionsResult;

  onMounted(async () => {
    const pageSize = await getDefaultPageSize();
    assetPage.pageSize = pageSize;
    materialPage.pageSize = pageSize;
    pageSizeOptions.value = createPageSizeOptions(pageSize);
    activeSource.value = notificationSource(route.query.source, route.path);
    await loadData();
  });

  return {
    route,
    router,
    userStore,
    approvalActionAccess,
    canViewMaterialFlow,
    loading,
    saving,
    withdrawingKeys,
    dialogVisible,
    progressVisible,
    selectedFlow,
    openedNotificationFlowKey,
    listRequestGuard,
    activeSource,
    assetFlows,
    materialFlows,
    assetTotal,
    materialTotal,
    assets,
    assetOptionsLoading,
    assetSearchKeyword,
    assetOptionsRequestGuard,
    users,
    userOptionsLoading,
    userOptionsRequestGuard,
    pageSizeOptions,
    form,
    formModel,
    query,
    queryModel,
    showReturnDate,
    assetSelectCopy,
    selectedAsset,
    transfereeOptions,
    inheritedReturnDate,
    disableReturnDate,
    assetPage,
    materialPage,
    currentPage,
    currentPageModel,
    flows,
    currentTotal,
    loadData,
    clearNotificationFlowQuery,
    openStart,
    searchAssets,
    searchUsers,
    onBizTypeChange,
    onAssetChange,
    submit,
    withdraw,
    onPageSizeChange,
    search,
    resetQuery,
    onSourceChange,
    showProgress,
    focusNotificationFlow,
  };
}
