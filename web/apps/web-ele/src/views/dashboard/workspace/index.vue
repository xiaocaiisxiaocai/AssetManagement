<script lang="ts" setup>
import { ElAlert, ElButton } from 'element-plus';

import AssetOverviewPanel from './AssetOverviewPanel.vue';
import OverduePanel from './OverduePanel.vue';
import ShortcutsPanel from './ShortcutsPanel.vue';
import StatCards from './StatCards.vue';
import TodoPanel from './TodoPanel.vue';
import { useWorkspace } from './use-workspace';

defineOptions({ name: 'Workspace' });

const {
  canHandleApprovals,
  canViewAnyApplications,
  canHandleAnyApprovals,
  pendingApprovalTarget,
  myApplicationTarget,
  canConfirmReturns,
  canViewReports,
  loading,
  summary,
  overdueRows,
  overdueTotal,
  pendingReturns,
  failedLoadLabels,
  initialLoading,
  pendingMineCount,
  pendingApprovalCount,
  categoryTopRows,
  metricCards,
  shortcuts,
  loadData,
  go,
  goCategoryAssets,
} = useWorkspace();
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

      <StatCards
        :initial-loading="initialLoading"
        :metric-cards="metricCards"
        @navigate="go"
      />

      <section class="workspace-dashboard page-fill">
        <div class="workspace-side">
          <TodoPanel
            :can-confirm-returns="canConfirmReturns"
            :can-handle-any-approvals="canHandleAnyApprovals"
            :can-handle-approvals="canHandleApprovals"
            :can-view-any-applications="canViewAnyApplications"
            :can-view-reports="canViewReports"
            :my-application-target="myApplicationTarget"
            :overdue-total="overdueTotal"
            :pending-approval-count="pendingApprovalCount"
            :pending-approval-target="pendingApprovalTarget"
            :pending-mine-count="pendingMineCount"
            :pending-returns="pendingReturns"
            @navigate="go"
          />
          <ShortcutsPanel :shortcuts="shortcuts" @navigate="go" />
        </div>

        <AssetOverviewPanel
          v-if="canViewReports"
          :category-top-rows="categoryTopRows"
          :summary="summary"
          @category-click="goCategoryAssets"
          @navigate="go"
        />

        <OverduePanel
          v-if="canViewReports"
          :overdue-rows="overdueRows"
          @navigate="go"
        />
      </section>
    </div>
  </re-page>
</template>

<style scoped src="./index.css"></style>
