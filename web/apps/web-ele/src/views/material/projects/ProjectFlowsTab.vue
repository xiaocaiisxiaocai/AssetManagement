<script lang="ts" setup>
import type { MaterialFlowItem } from '#/api/material';

import { ElTabPane, ElTabs } from 'element-plus';

import ProjectHandledFlowsPane from './ProjectHandledFlowsPane.vue';
import ProjectMyFlowsPane from './ProjectMyFlowsPane.vue';
import ProjectPendingFlowsPane from './ProjectPendingFlowsPane.vue';

defineProps<{
  canApprove: boolean;
  handledCount: number;
  handledFlows: MaterialFlowItem[];
  handledLoading: boolean;
  myCount: number;
  myFlows: MaterialFlowItem[];
  myLoading: boolean;
  pageSizeOptions: number[];
  pendingCount: number;
  pendingFlows: MaterialFlowItem[];
  pendingLoading: boolean;
  processingFlowIds: number[];
  readOnly: boolean;
}>();

const emit = defineEmits<{
  approve: [flow: MaterialFlowItem];
  handledPageSizeChange: [];
  myPageSizeChange: [];
  pageChange: [];
  pendingPageSizeChange: [];
  reject: [flow: MaterialFlowItem];
  tabChange: [];
  withdraw: [flow: MaterialFlowItem];
}>();
const activeTab = defineModel<string>('activeTab', { required: true });
const handledQuery = defineModel<{ page: number; pageSize: number }>(
  'handledQuery',
  { required: true },
);
const myQuery = defineModel<{ page: number; pageSize: number }>('myQuery', {
  required: true,
});
const pendingQuery = defineModel<{ page: number; pageSize: number }>(
  'pendingQuery',
  { required: true },
);
</script>

<template>
  <ElTabPane name="flows">
    <template #label>
      <span>流转审批</span>
      <span class="tab-count">{{ pendingCount + handledCount + myCount }}</span>
    </template>
    <ElTabs
      v-model="activeTab"
      class="inner-flow-tabs"
      @tab-change="emit('tabChange')"
    >
      <ElTabPane v-if="canApprove" name="pending">
        <template #label>待我审批 {{ pendingCount }}</template>
        <ProjectPendingFlowsPane
          v-model:pending-query="pendingQuery"
          :page-size-options="pageSizeOptions"
          :pending-count="pendingCount"
          :pending-flows="pendingFlows"
          :pending-loading="pendingLoading"
          :processing-flow-ids="processingFlowIds"
          @approve="emit('approve', $event)"
          @page-change="emit('pageChange')"
          @pending-page-size-change="emit('pendingPageSizeChange')"
          @reject="emit('reject', $event)"
        />
      </ElTabPane>
      <ElTabPane v-if="canApprove" name="handled">
        <template #label>我已处理 {{ handledCount }}</template>
        <ProjectHandledFlowsPane
          v-model:handled-query="handledQuery"
          :handled-count="handledCount"
          :handled-flows="handledFlows"
          :handled-loading="handledLoading"
          :page-size-options="pageSizeOptions"
          @handled-page-size-change="emit('handledPageSizeChange')"
          @page-change="emit('pageChange')"
        />
      </ElTabPane>
      <ElTabPane name="mine">
        <template #label>我的发起 {{ myCount }}</template>
        <ProjectMyFlowsPane
          v-model:my-query="myQuery"
          :my-count="myCount"
          :my-flows="myFlows"
          :my-loading="myLoading"
          :page-size-options="pageSizeOptions"
          :read-only="readOnly"
          :processing-flow-ids="processingFlowIds"
          @my-page-size-change="emit('myPageSizeChange')"
          @page-change="emit('pageChange')"
          @withdraw="emit('withdraw', $event)"
        />
      </ElTabPane>
    </ElTabs>
  </ElTabPane>
</template>

<style scoped src="./ProjectFlowsTab.css"></style>
