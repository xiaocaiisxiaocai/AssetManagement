<script lang="ts" setup>
import AssetBorrowDialog from './components/AssetBorrowDialog.vue';
import AssetCategoryList from './components/AssetCategoryList.vue';
import AssetDetailDialog from './components/AssetDetailDialog.vue';
import AssetFormDialog from './components/AssetFormDialog.vue';
import AssetImportDialog from './components/AssetImportDialog.vue';
import AssetRootGrid from './components/AssetRootGrid.vue';
import AssetTablePanel from './components/AssetTablePanel.vue';
import AssetTransferDialog from './components/AssetTransferDialog.vue';
import AssetWorkspaceHeader from './components/AssetWorkspaceHeader.vue';
import { useAssetListPage } from './use-asset-list-page';

defineOptions({ name: 'AssetList' });

const {
  currentUserId,
  MAX_CATEGORY_LEVEL,
  loading,
  deletingAssetIds,
  dialogVisible,
  importDialogVisible,
  borrowDialogVisible,
  transferDialogVisible,
  editingAsset,
  formDefaultCategoryId,
  flatMode,
  assets,
  categoryAssetCounts,
  total,
  categoryPath,
  categoryPage,
  categoryPageSize,
  hierarchyKeyword,
  users,
  currentAssetForAction,
  detailVisible,
  detailLoading,
  userOptionsLoading,
  searchUsers,
  detail,
  exportingAllAssets,
  pageSizeOptions,
  queryModel,
  categoryOptions,
  activeDepartmentOptions,
  hierarchyNodes,
  hierarchyParent,
  hierarchyTrail,
  currentCategoryLevel,
  isCategoryStage,
  showAssetTable,
  currentLevelTitle,
  nextLevelName,
  filteredHierarchyNodes,
  pagedHierarchyNodes,
  canPurgeAsset,
  canRestoreAsset,
  assetRowActionAccess,
  showAllAssetExport,
  loadData,
  resetQuery,
  search,
  enterFlatMode,
  exitFlatMode,
  openCreate,
  openEdit,
  openDetail,
  onSaved,
  onImported,
  onRowCommand,
  drillIntoCategory,
  drillToCategoryPath,
  exportAllAssets,
  resetCategorySearch,
} = useAssetListPage();
</script>

<template>
  <re-page>
    <div class="asset-list-page page-fit p-5">
      <section class="asset-workspace page-fill">
        <AssetWorkspaceHeader
          :access="assetRowActionAccess"
          :category-path="categoryPath"
          :current-category-level="currentCategoryLevel"
          :current-level-title="currentLevelTitle"
          :exporting-all-assets="exportingAllAssets"
          :flat-mode="flatMode"
          :hierarchy-parent="hierarchyParent"
          :hierarchy-trail="hierarchyTrail"
          :show-all-asset-export="showAllAssetExport"
          :show-asset-table="showAssetTable"
          @create="openCreate(hierarchyParent?.id)"
          @enter-flat-mode="enterFlatMode"
          @exit-flat-mode="exitFlatMode"
          @export-all="exportAllAssets"
          @import="importDialogVisible = true"
          @navigate="drillToCategoryPath"
        />

        <AssetRootGrid
          v-if="!showAssetTable && currentCategoryLevel === 0"
          :hierarchy-nodes="hierarchyNodes"
          @enter="drillIntoCategory"
        />

        <AssetCategoryList
          v-else-if="!showAssetTable && isCategoryStage"
          v-model:category-page="categoryPage"
          v-model:category-page-size="categoryPageSize"
          v-model:hierarchy-keyword="hierarchyKeyword"
          :category-asset-counts="categoryAssetCounts"
          :current-category-level="currentCategoryLevel"
          :filtered-hierarchy-nodes="filteredHierarchyNodes"
          :max-category-level="MAX_CATEGORY_LEVEL"
          :next-level-name="nextLevelName"
          :page-size-options="pageSizeOptions"
          :paged-hierarchy-nodes="pagedHierarchyNodes"
          @enter="drillIntoCategory"
          @reset="resetCategorySearch"
        />

        <AssetTablePanel
          v-else
          v-model:query="queryModel"
          :access="assetRowActionAccess"
          :assets="assets"
          :can-purge-asset="canPurgeAsset"
          :can-restore-asset="canRestoreAsset"
          :current-user-id="currentUserId"
          :deleting-asset-ids="deletingAssetIds"
          :department-options="activeDepartmentOptions"
          :loading="loading"
          :page-size-options="pageSizeOptions"
          :total="total"
          :user-options-loading="userOptionsLoading"
          :users="users"
          @command="onRowCommand"
          @detail="openDetail"
          @edit="openEdit"
          @page-change="loadData"
          @reset="resetQuery"
          @search="search"
          @user-search="searchUsers"
        />
      </section>

      <AssetFormDialog
        v-model:visible="dialogVisible"
        :asset="editingAsset"
        :category-options="categoryOptions"
        :default-category-id="formDefaultCategoryId"
        :department-options="activeDepartmentOptions"
        :search-users="searchUsers"
        :user-options-loading="userOptionsLoading"
        :users="users"
        @saved="onSaved"
      />

      <AssetImportDialog
        v-model:visible="importDialogVisible"
        @imported="onImported"
      />

      <AssetDetailDialog
        v-model:visible="detailVisible"
        :detail="detail"
        :loading="detailLoading"
      />

      <AssetBorrowDialog
        v-model:visible="borrowDialogVisible"
        :asset="currentAssetForAction"
      />

      <AssetTransferDialog
        v-model:visible="transferDialogVisible"
        :asset="currentAssetForAction"
        :search-users="searchUsers"
        :user-options-loading="userOptionsLoading"
        :users="users"
      />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
