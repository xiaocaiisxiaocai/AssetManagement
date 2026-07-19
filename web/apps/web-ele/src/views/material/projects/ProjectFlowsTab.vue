<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import WorkflowProgressSummary from '#/components/workflow/WorkflowProgressSummary.vue';
import { canWithdrawMaterialFlow } from './project-workspace-rules';

import {
  ElButton,
  ElOption,
  ElPagination,
  ElSelect,
  ElTabPane,
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTag,
} from 'element-plus';

defineProps<{
  canApprove: boolean;
  myCount: number;
  myFlows: MaterialFlowItem[];
  myLoading: boolean;
  myQuery: { page: number; pageSize: number };
  pageSizeOptions: number[];
  pendingCount: number;
  pendingFlows: MaterialFlowItem[];
  pendingLoading: boolean;
  pendingQuery: { page: number; pageSize: number };
  readOnly: boolean;
}>();

const emit = defineEmits<{
  approve: [flow: MaterialFlowItem];
  myPageSizeChange: [];
  pendingPageSizeChange: [];
  reject: [flow: MaterialFlowItem];
  tabChange: [];
  withdraw: [flow: MaterialFlowItem];
}>();
const activeTab = defineModel<string>('activeTab', { required: true });

const flowStatusMeta: Record<
  string,
  { label: string; tag: 'info' | 'success' | 'warning' }
> = {
  approved: { label: '已通过', tag: 'success' },
  pending: { label: '审批中', tag: 'warning' },
  rejected: { label: '已驳回', tag: 'info' },
  withdrawn: { label: '已撤回', tag: 'info' },
};

function flowMetaOf(status: string) {
  return flowStatusMeta[status] ?? { label: status, tag: 'info' as const };
}
</script>

<template>
  <ElTabPane name="flows">
    <template #label>
      <span>流转审批</span>
      <span class="tab-count">{{ pendingCount + myCount }}</span>
    </template>
    <ElTabs
      v-model="activeTab"
      class="inner-flow-tabs"
      @tab-change="emit('tabChange')"
    >
      <ElTabPane v-if="canApprove" name="pending">
        <template #label>待我审批 {{ pendingCount }}</template>
        <div class="drawer-table-panel flow-table-panel">
          <ElTable
            v-loading="pendingLoading"
            :data="pendingFlows"
            border
            height="100%"
            stripe
          >
            <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
            <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
            <ElTableColumn
              label="料件名称"
              min-width="140"
              prop="materialName"
              show-overflow-tooltip
            />
            <ElTableColumn label="发起人" min-width="90" prop="applicant" />
            <ElTableColumn label="受让人" min-width="90" prop="transferee" />
            <ElTableColumn
              label="原因"
              min-width="150"
              prop="reason"
              show-overflow-tooltip
            />
            <ElTableColumn label="审批进度" min-width="320">
              <template #default="{ row }">
                <WorkflowProgressSummary
                  :current-steps="row.currentSteps"
                  :next-steps="row.nextSteps"
                  :status="row.status"
                />
              </template>
            </ElTableColumn>
            <ElTableColumn align="center" label="操作" width="140">
              <template #default="{ row }">
                <ElButton
                  :disabled="row.actionableNodeIds.length === 0"
                  link
                  size="small"
                  type="success"
                  @click="emit('approve', row)"
                  >通过</ElButton
                >
                <ElButton
                  :disabled="row.actionableNodeIds.length === 0"
                  link
                  size="small"
                  type="danger"
                  @click="emit('reject', row)"
                  >驳回</ElButton
                >
              </template>
            </ElTableColumn>
          </ElTable>
          <div class="table-bottom-pager">
            <div class="table-bottom-pager-left">
              <span>共 {{ pendingCount }} 条记录</span
              ><span class="table-bottom-pager-divider">|</span
              ><span>每页</span>
              <ElSelect
                v-model="pendingQuery.pageSize"
                style="width: 92px"
                @change="emit('pendingPageSizeChange')"
              >
                <ElOption
                  v-for="size in pageSizeOptions"
                  :key="size"
                  :label="`${size}`"
                  :value="size"
                />
              </ElSelect>
            </div>
            <ElPagination
              v-model:current-page="pendingQuery.page"
              :page-size="pendingQuery.pageSize"
              :total="pendingCount"
              background
              layout="prev, pager, next"
            />
          </div>
        </div>
      </ElTabPane>
      <ElTabPane name="mine">
        <template #label>我的发起 {{ myCount }}</template>
        <div class="drawer-table-panel flow-table-panel">
          <ElTable
            v-loading="myLoading"
            :data="myFlows"
            border
            height="100%"
            stripe
          >
            <ElTableColumn label="流转单号" min-width="170" prop="flowNo" />
            <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
            <ElTableColumn
              label="料件名称"
              min-width="140"
              prop="materialName"
              show-overflow-tooltip
            />
            <ElTableColumn label="受让人" min-width="90" prop="transferee" />
            <ElTableColumn
              label="原因"
              min-width="150"
              prop="reason"
              show-overflow-tooltip
            />
            <ElTableColumn align="center" label="状态" width="110">
              <template #default="{ row }"
                ><ElTag :type="flowMetaOf(row.status).tag" size="small">{{
                  flowMetaOf(row.status).label
                }}</ElTag></template
              >
            </ElTableColumn>
            <ElTableColumn label="审批进度" min-width="320">
              <template #default="{ row }">
                <WorkflowProgressSummary
                  :current-steps="row.currentSteps"
                  :next-steps="row.nextSteps"
                  :status="row.status"
                />
              </template>
            </ElTableColumn>
            <ElTableColumn align="center" fixed="right" label="操作" width="90">
              <template #default="{ row }">
                <ElButton
                  v-if="!readOnly && canWithdrawMaterialFlow(row)"
                  link
                  size="small"
                  type="danger"
                  @click="emit('withdraw', row)"
                  >撤回</ElButton
                >
              </template>
            </ElTableColumn>
          </ElTable>
          <div class="table-bottom-pager">
            <div class="table-bottom-pager-left">
              <span>共 {{ myCount }} 条记录</span
              ><span class="table-bottom-pager-divider">|</span
              ><span>每页</span>
              <ElSelect
                v-model="myQuery.pageSize"
                style="width: 92px"
                @change="emit('myPageSizeChange')"
              >
                <ElOption
                  v-for="size in pageSizeOptions"
                  :key="size"
                  :label="`${size}`"
                  :value="size"
                />
              </ElSelect>
            </div>
            <ElPagination
              v-model:current-page="myQuery.page"
              :page-size="myQuery.pageSize"
              :total="myCount"
              background
              layout="prev, pager, next"
            />
          </div>
        </div>
      </ElTabPane>
    </ElTabs>
  </ElTabPane>
</template>

<style scoped>
.tab-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 18px;
  padding: 0 6px;
  margin-left: 6px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
  background: var(--el-fill-color-light);
  border-radius: 999px;
}
.drawer-table-panel {
  display: flex;
  flex: 1;
  min-height: 0;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  background: var(--asset-page-surface);
}
.drawer-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
}
.flow-table-panel {
  flex: 1;
  height: auto;
  min-height: 0;
}
.inner-flow-tabs {
  display: flex;
  flex: 1;
  height: auto;
  min-height: 0;
  flex-direction: column;
}
.inner-flow-tabs :deep(.el-tabs__content) {
  order: 1;
  flex: 1;
  min-height: 0;
}
.inner-flow-tabs :deep(.el-tabs__header) {
  order: 0;
  flex-shrink: 0;
  margin-bottom: 12px;
}
.inner-flow-tabs :deep(.el-tab-pane) {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}
.table-bottom-pager {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-top: 1px solid var(--asset-page-border);
  background: var(--asset-page-surface);
}
.table-bottom-pager-left {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  color: var(--asset-page-muted);
  font-size: 14px;
  line-height: 20px;
}
.table-bottom-pager-divider {
  color: var(--asset-page-border);
}
@media (max-width: 768px) {
  .flow-table-panel,
  .inner-flow-tabs {
    height: auto;
  }
}
</style>
