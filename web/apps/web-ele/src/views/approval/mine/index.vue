<script lang="ts" setup>
import ApprovalProgressDialog from './ApprovalProgressDialog.vue';
import MineFilter from './MineFilter.vue';
import MineHeader from './MineHeader.vue';
import MineTable from './MineTable.vue';
import StartApprovalDialog from './StartApprovalDialog.vue';
import { useMineApprovals } from './use-mine-approvals';

defineOptions({ name: 'ApprovalMine' });

const {
  approvalActionAccess,
  canViewMaterialFlow,
  loading,
  saving,
  withdrawingKeys,
  dialogVisible,
  progressVisible,
  selectedFlow,
  activeSource,
  assets,
  assetOptionsLoading,
  userOptionsLoading,
  pageSizeOptions,
  formModel,
  queryModel,
  showReturnDate,
  assetSelectCopy,
  transfereeOptions,
  inheritedReturnDate,
  disableReturnDate,
  currentPageModel,
  flows,
  currentTotal,
  loadData,
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
} = useMineApprovals();
</script>

<template>
  <re-page>
    <div class="mine-page page-fit">
      <MineHeader
        :can-create="approvalActionAccess.canCreate"
        @start="openStart"
      />

      <MineFilter
        v-model:active-source="activeSource"
        v-model:query="queryModel"
        :can-view-material-flow="canViewMaterialFlow"
        @reset="resetQuery"
        @search="search"
        @source-change="onSourceChange"
      />

      <MineTable
        v-model:filters="queryModel"
        v-model:query="currentPageModel"
        :active-source="activeSource"
        :flows="flows"
        :loading="loading"
        :page-size-options="pageSizeOptions"
        :total="currentTotal"
        :withdrawing-keys="withdrawingKeys"
        @page-change="loadData"
        @search="search"
        @page-size-change="onPageSizeChange"
        @show-progress="showProgress"
        @withdraw="withdraw"
      />

      <ApprovalProgressDialog
        v-model:visible="progressVisible"
        :selected-flow="selectedFlow"
      />

      <StartApprovalDialog
        v-model:form="formModel"
        v-model:visible="dialogVisible"
        :asset-options-loading="assetOptionsLoading"
        :asset-select-copy="assetSelectCopy"
        :assets="assets"
        :disable-return-date="disableReturnDate"
        :inherited-return-date="inheritedReturnDate"
        :saving="saving"
        :show-return-date="showReturnDate"
        :transferee-options="transfereeOptions"
        :user-options-loading="userOptionsLoading"
        @asset-change="onAssetChange"
        @asset-search="searchAssets"
        @biz-type-change="onBizTypeChange"
        @submit="submit"
        @user-search="searchUsers"
      />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
