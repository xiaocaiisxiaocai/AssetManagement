<script lang="ts" setup>
import WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';

import AddSignDialog from './AddSignDialog.vue';
import ApprovalDetailDialog from './ApprovalDetailDialog.vue';
import HandledDetailDialog from './HandledDetailDialog.vue';
import PendingTable from './PendingTable.vue';
import PendingToolbar from './PendingToolbar.vue';
import { usePendingApprovals } from './use-pending-approvals';

defineOptions({ name: 'ApprovalPending' });

const {
  canHandleMaterialFlow,
  loading,
  actionLoading,
  addSignLoading,
  cancelAddSignLoadingKey,
  detailVisible,
  addSignVisible,
  selected,
  handledSelected,
  handledDetailVisible,
  activeSource,
  viewMode,
  materialActionLoadingIds,
  pageSizeOptions,
  opinion,
  addSignUser,
  selectedNodeId,
  workflowNodeSelector,
  queryModel,
  currentPageModel,
  flows,
  currentTotal,
  addSignCandidates,
  loadData,
  openDetail,
  openHandledDetail,
  currentNodeInfo,
  activeNodeOptions,
  currentSignStates,
  canCurrentUserAddSign,
  cancelAddSign,
  openAddSign,
  addSign,
  approve,
  reject,
  approveMaterial,
  rejectMaterial,
  onPageSizeChange,
  search,
  resetQuery,
  onSourceChange,
  onViewModeChange,
} = usePendingApprovals();
</script>

<template>
  <re-page>
    <div class="pending-page">
      <PendingToolbar
        v-model:active-source="activeSource"
        v-model:query="queryModel"
        v-model:view-mode="viewMode"
        :can-handle-material-flow="canHandleMaterialFlow"
        @reset="resetQuery"
        @search="search"
        @source-change="onSourceChange"
        @view-mode-change="onViewModeChange"
      />

      <PendingTable
        v-model:query="currentPageModel"
        :flows="flows"
        :loading="loading"
        :material-action-loading-ids="materialActionLoadingIds"
        :page-size-options="pageSizeOptions"
        :total="currentTotal"
        :view-mode="viewMode"
        @approve-material="approveMaterial"
        @open-detail="openDetail"
        @open-handled-detail="openHandledDetail"
        @page-change="loadData"
        @page-size-change="onPageSizeChange"
        @reject-material="rejectMaterial"
      />

      <ApprovalDetailDialog
        v-model:opinion="opinion"
        v-model:selected-node-id="selectedNodeId"
        v-model:visible="detailVisible"
        :action-loading="actionLoading"
        :active-node-options="activeNodeOptions"
        :add-sign-loading="addSignLoading"
        :can-current-user-add-sign="canCurrentUserAddSign"
        :cancel-add-sign-loading-key="cancelAddSignLoadingKey"
        :current-node-info="currentNodeInfo"
        :current-sign-states="currentSignStates"
        :selected="selected"
        @approve="approve"
        @cancel-add-sign="cancelAddSign"
        @open-add-sign="openAddSign"
        @reject="reject"
      />

      <HandledDetailDialog
        v-model:visible="handledDetailVisible"
        :handled-selected="handledSelected"
      />

      <AddSignDialog
        v-model:add-sign-user="addSignUser"
        v-model:visible="addSignVisible"
        :candidates="addSignCandidates"
        :loading="addSignLoading"
        @confirm="addSign"
      />

      <WorkflowNodeSelectDialog ref="workflowNodeSelector" />
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
