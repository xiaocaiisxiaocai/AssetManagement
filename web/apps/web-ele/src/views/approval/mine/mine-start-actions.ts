import { ElMessage } from 'element-plus';

import { getAssetListApi } from '#/api/asset';
import { getUserOptionsPageApi } from '#/api/user';
import { startApprovalApi } from '#/api/workflow';
import {
  disableNonExtensionReturnDate,
  disableNonFutureReturnDate,
  isFutureReturnDate,
  isValidExtensionReturnDate,
} from '#/utils/return-date';
import { replaceUserOptions } from '#/utils/user-options';

import {
  buildApprovalAssetQuery,
  mergeApprovalAssetOptions,
} from './approval-asset-options';
import { validateApprovalReason } from './approval-form-rules';

export function bindMineStartActions(page: Record<string, any>) {
  const {
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
  } = page;

  function disableReturnDate(time: Date) {
    return form.bizType === 'extension'
      ? disableNonExtensionReturnDate(
          time,
          selectedAsset.value?.returnDate || '',
        )
      : disableNonFutureReturnDate(time);
  }

  async function openStart(type = 'borrow') {
    Object.assign(form, {
      assetId: undefined,
      bizType: type,
      reason: '',
      returnDate: '',
      transfereeId: undefined,
    });
    assets.value = [];
    assetSearchKeyword.value = '';
    dialogVisible.value = true;
    await Promise.all([
      searchAssets(''),
      type === 'transfer' && users.value.length === 0
        ? searchUsers('')
        : Promise.resolve(),
    ]);
  }

  async function searchAssets(keyword = '') {
    const generation = assetOptionsRequestGuard.next();
    assetSearchKeyword.value = keyword.trim();
    assetOptionsLoading.value = true;
    try {
      const currentUserId = Number(userStore.userInfo?.userId || 0);
      const result = await getAssetListApi(
        buildApprovalAssetQuery(form.bizType, currentUserId, keyword),
      );
      if (!assetOptionsRequestGuard.isLatest(generation)) return;
      assets.value = mergeApprovalAssetOptions(
        assets.value,
        result.items,
        form.assetId,
      );
    } catch {
      // 请求层已提示，保留当前选项。
    } finally {
      if (assetOptionsRequestGuard.isLatest(generation))
        assetOptionsLoading.value = false;
    }
  }

  async function searchUsers(keyword = '') {
    const requestGeneration = userOptionsRequestGuard.next();
    userOptionsLoading.value = true;
    try {
      const result = await getUserOptionsPageApi(keyword, 1, 50);
      if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
      users.value = replaceUserOptions(users.value, result.items, [
        form.transfereeId,
      ]);
    } catch {
      // 请求层已提示，保留已回填选项。
    } finally {
      if (userOptionsRequestGuard.isLatest(requestGeneration))
        userOptionsLoading.value = false;
    }
  }

  async function onBizTypeChange() {
    form.assetId = undefined;
    assets.value = [];
    assetSearchKeyword.value = '';
    await searchAssets('');
    if (form.bizType === 'transfer' && users.value.length === 0) {
      await searchUsers('');
    }
  }

  function onAssetChange() {
    form.returnDate = '';
  }

  async function submit() {
    if (!form.assetId) {
      ElMessage.warning('请选择资产');
      return;
    }
    if (showReturnDate.value && !form.returnDate) {
      ElMessage.warning('请选择归还日期');
      return;
    }
    if (showReturnDate.value && !isFutureReturnDate(form.returnDate)) {
      ElMessage.warning('归还日期必须晚于今天');
      return;
    }
    if (
      form.bizType === 'extension' &&
      (!selectedAsset.value?.returnDate ||
        !isValidExtensionReturnDate(
          form.returnDate,
          selectedAsset.value.returnDate,
        ))
    ) {
      ElMessage.warning('新归还日期必须晚于原应归还日期');
      return;
    }
    if (form.bizType === 'transfer' && !form.transfereeId) {
      ElMessage.warning('请选择接收人');
      return;
    }
    const reasonValidationMessage = validateApprovalReason(
      form.bizType,
      form.reason,
    );
    if (reasonValidationMessage) {
      ElMessage.warning(reasonValidationMessage);
      return;
    }
    saving.value = true;
    try {
      await startApprovalApi({
        assetId: form.assetId,
        bizType: form.bizType,
        reason: form.reason.trim() || undefined,
        returnDate: showReturnDate.value ? form.returnDate : undefined,
        transfereeId:
          form.bizType === 'transfer' ? form.transfereeId : undefined,
      });
      ElMessage.success('申请已提交');
      dialogVisible.value = false;
      activeSource.value = 'asset';
      assetPage.page = 1;
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    } finally {
      saving.value = false;
    }
  }

  return {
    disableReturnDate,
    openStart,
    searchAssets,
    searchUsers,
    onBizTypeChange,
    onAssetChange,
    submit,
  };
}
